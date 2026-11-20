using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Azure;
using Azure.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Workleap.DomainEventPropagation.EventGridClientAdapter;

using EventBundle = Workleap.DomainEventPropagation.EventGridClientAdapter.EventGridClientAdapter.EventBundle;

namespace Workleap.DomainEventPropagation;

internal sealed class EventPullerService : BackgroundService
{
    private readonly List<EventGridSubscriptionEventPuller> _eventGridSubscriptionPullers;

    public EventPullerService(
        IServiceScopeFactory serviceScopeFactory,
        IEnumerable<EventGridClientDescriptor> clientDescriptors,
        IEventGridClientWrapperFactory eventGridClientWrapperFactory,
        IOptionsMonitor<EventPropagationSubscriptionOptions> optionsMonitor,
        ILogger<EventPullerService> logger)
    {
        this._eventGridSubscriptionPullers = clientDescriptors.Select(descriptor =>
                new EventGridSubscriptionEventPuller(
                    new EventGridTopicSubscription(
                        optionsMonitor.Get(descriptor.Name).TopicName,
                        optionsMonitor.Get(descriptor.Name).SubscriptionName,
                        optionsMonitor.Get(descriptor.Name).MaxDegreeOfParallelism,
                        optionsMonitor.Get(descriptor.Name).MaxRetries,
                        optionsMonitor.Get(descriptor.Name).RetryDelays?.ToList(),
                        eventGridClientWrapperFactory.CreateClient(descriptor.Name)),
                    serviceScopeFactory,
                    logger))
            .ToList();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var taskCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var subscriptionTasks = this._eventGridSubscriptionPullers
            .Select(puller => Task.Run(() => puller.StartReceivingEventsAsync(taskCancellationSource.Token), taskCancellationSource.Token))
            .ToList();

        while (subscriptionTasks.Any())
        {
            var task = await Task.WhenAny(subscriptionTasks).ConfigureAwait(false);

            subscriptionTasks.Remove(task);

            try
            {
                // This will throw the exception if there is one, otherwise we'll continue until all tasks are done
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // To prevent unobserved exceptions, we cancel the tasks and wait for them to complete before throwing
                await taskCancellationSource.CancelAsync().ConfigureAwait(false);
                await Task.WhenAll(subscriptionTasks).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

                throw;
            }
        }
    }

    private class EventGridSubscriptionEventPuller
    {
        // At the moment the Release api only supports these delays
        // See https://learn.microsoft.com/en-us/dotnet/api/azure.messaging.eventgrid.namespaces.eventgridclient.releasecloudeventsasync?view=azure-dotnet-preview#azure-messaging-eventgrid-namespaces-eventgridclient-releasecloudeventsasync(system-string-system-string-azure-core-requestcontent-system-nullable((system-int32))-azure-requestcontext)
        private static readonly TimeSpan[] SupportedReleaseDelays = [TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(600), TimeSpan.FromSeconds(3600)];

        private static readonly TimeSpan InitialReconnectDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromMinutes(1);
        private static readonly DelayStrategy ReconnectDelayStrategy = DelayStrategy.CreateExponentialDelayStrategy(InitialReconnectDelay, MaxReconnectDelay);

        private const int OutputChannelSize = 5000;
        private const int MaxEventRequestSize = 100;

        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<EventPullerService> _logger;
        private readonly EventGridTopicSubscription _eventGridTopicSubscription;

        private readonly Dictionary<int, TimeSpan>? _retryDelays;
        private readonly TimeSpan? _defaultDelay;

        // Recreated on every reconnection because a completed channel can't be reopened
        private Channel<EventBundle> _acknowledgeEventChannel = Channel.CreateBounded<EventBundle>(OutputChannelSize);
        private Channel<EventBundle> _releaseEventChannel = Channel.CreateBounded<EventBundle>(OutputChannelSize);
        private Channel<EventBundle> _rejectEventChannel = Channel.CreateBounded<EventBundle>(OutputChannelSize);

        private int _handlersInProgress;
        private TaskCompletionSource _taskCompletionSource = new();

        public EventGridSubscriptionEventPuller(
            EventGridTopicSubscription eventGridTopicSubscription,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<EventPullerService> logger)
        {
            this._serviceScopeFactory = serviceScopeFactory;
            this._logger = logger;
            this._eventGridTopicSubscription = eventGridTopicSubscription;

            if (eventGridTopicSubscription.RetryDelays is { Count: > 0 })
            {
                this._retryDelays = eventGridTopicSubscription.RetryDelays
                    .Select((x, index) => new { DeliveryCount = index + 1, Delay = x })
                    .ToDictionary(x => x.DeliveryCount, x => x.Delay);

                this._defaultDelay = eventGridTopicSubscription.RetryDelays.Last();
            }
        }

        public async Task StartReceivingEventsAsync(CancellationToken cancellationToken)
        {
            var failureCount = 0;

            while (true)
            {
                var receivingStartedAt = Stopwatch.GetTimestamp();

                try
                {
                    await this.ReceiveEventsAsync(cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex) when (IsEventGridUnavailable(ex) && !cancellationToken.IsCancellationRequested)
                {
                    // An Event Grid outage must not stop the host: it would take down every other part of the application with it
                    // Settlement calls can keep failing while receiving succeeds, so only a session that stayed up resets the backoff
                    failureCount = Stopwatch.GetElapsedTime(receivingStartedAt) >= MaxReconnectDelay ? 1 : failureCount + 1;
                    var reconnectDelay = GetReconnectDelay(failureCount);

                    this._logger.EventGridUnavailable(this._eventGridTopicSubscription.TopicName, this._eventGridTopicSubscription.SubscriptionName, failureCount, reconnectDelay, ex);

                    await Task.Delay(reconnectDelay, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                    this.ResetEventChannels();
                }
            }
        }

        private static bool IsEventGridUnavailable(Exception ex) => ex switch
        {
            // 0 means no response was received. Other client errors (401, 403, 404) are configuration mistakes that must still stop the host
            RequestFailedException { Status: 0 or 408 or 429 or >= 500 } => true,

            // The Azure SDK reports a network timeout as a TaskCanceledException. The caller only retries while the host isn't stopping
            OperationCanceledException => true,

            // The Azure SDK retry policy wraps the failure of each attempt in an AggregateException
            AggregateException aggregate => aggregate.InnerExceptions.All(IsEventGridUnavailable),
            _ => false,
        };

        // Jittered so that every pod failing at once doesn't hit Event Grid in lockstep when it recovers
        // The retry number is capped because the strategy computes 1 << (retryNumber - 1), which overflows after 31 failures
        private static TimeSpan GetReconnectDelay(int failureCount) => ReconnectDelayStrategy.GetNextDelay(response: null, retryNumber: Math.Min(failureCount, 10));

        private async Task ReceiveEventsAsync(CancellationToken cancellationToken)
        {
            // Use a local cancellation token source to allow stopping the events reception/processing if a callback task crashes
            using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Start the tasks that will process events and handle the completion callback
            Task[] eventProcessingTasks =
            [
                this.ProcessEvents(cancellationTokenSource.Token),
                this.AcknowledgeEvents(cancellationToken),
                this.ReleaseEvents(cancellationToken),
                this.RejectEvents(cancellationToken)
            ];

            await Task.WhenAny(eventProcessingTasks).ConfigureAwait(false);

            // If any task completed, we want to stop receiving/processing events, flush the channels and then throw (if needed)
            await cancellationTokenSource.CancelAsync().ConfigureAwait(false);
            this.CompleteEventChannels();

            // Since the event processing task was cancelled and the callback channels completed, all the tasks should complete shortly
            var allTasks = Task.WhenAll(eventProcessingTasks);
            try
            {
                await allTasks.ConfigureAwait(false);
            }
            catch (Exception) when (allTasks.Exception is { InnerExceptions.Count: > 1 } failures)
            {
                // await only surfaces the first failure, which could hide an unrelated one behind an Event Grid outage and retry it away
                throw failures;
            }
        }

        private Task ProcessEvents(CancellationToken cancellationToken)
        {
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = this._eventGridTopicSubscription.MaxHandlerDop, CancellationToken = cancellationToken };

            return Parallel.ForEachAsync(this.StreamEventGridEvents(cancellationToken), parallelOptions, async (bundle, ctx) =>
            {
                this.SignalHandlerStarting();

                try
                {
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
                    await using var scope = this._serviceScopeFactory.CreateAsyncScope();
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
                    var cloudEventHandler = scope.ServiceProvider.GetRequiredService<ICloudEventHandler>();

                    await this.HandleBundleAsync(cloudEventHandler, bundle, ctx).ConfigureAwait(false);
                }
                finally
                {
                    this.SignalHandlerCompleted();
                }
            });
        }

        private async IAsyncEnumerable<EventBundle> StreamEventGridEvents([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await this.WaitForAvailableHandler(cancellationToken).ConfigureAwait(false);

                var availableHandlers = this._eventGridTopicSubscription.MaxHandlerDop - this._handlersInProgress;
                if (availableHandlers <= 0)
                {
                    continue;
                }

                var bundles = await this._eventGridTopicSubscription.Client.ReceiveCloudEventsAsync(this._eventGridTopicSubscription.TopicName, this._eventGridTopicSubscription.SubscriptionName, Math.Min(availableHandlers, MaxEventRequestSize), cancellationToken).ConfigureAwait(false);

                foreach (var bundle in bundles)
                {
                    yield return bundle;
                }
            }
        }

        private async Task HandleBundleAsync(ICloudEventHandler cloudEventHandler, EventBundle eventBundle, CancellationToken cancellationToken)
        {
            var isLastDelivery = eventBundle.DeliveryCount > this._eventGridTopicSubscription.MaxRetriesCount;

            try
            {
                await cloudEventHandler.HandleCloudEventAsync(eventBundle.Event, eventBundle.DeliveryCount, isLastDelivery, cancellationToken).ConfigureAwait(false);
                await this._acknowledgeEventChannel.Writer.WriteAsync(eventBundle, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                switch (ex)
                {
                    case DomainEventTypeNotRegisteredException:
                    case CloudEventSerializationException:
                    case DomainEventHandlerNotRegisteredException:
                        this._logger.EventWillBeRejected(eventBundle.Event.Id, eventBundle.Event.Type, ex);
                        await this._rejectEventChannel.Writer.WriteAsync(eventBundle, cancellationToken).ConfigureAwait(false);
                        break;
                    case not null when isLastDelivery:
                        this._logger.EventWillBeRejectedDueToMaxRetries(eventBundle.Event.Id, eventBundle.Event.Type, ex);
                        await this._rejectEventChannel.Writer.WriteAsync(eventBundle, cancellationToken).ConfigureAwait(false);
                        break;
                    default:
                        this._logger.EventWillBeReleased(eventBundle.Event.Id, eventBundle.Event.Type, ex!);
                        await this._releaseEventChannel.Writer.WriteAsync(eventBundle, cancellationToken).ConfigureAwait(false);
                        break;
                }
            }
        }

        private async Task AcknowledgeEvents(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && !this._acknowledgeEventChannel.Reader.Completion.IsCompleted)
            {
                if (!await this._acknowledgeEventChannel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return;
                }

                var lockTokens = ReadCurrentContent(this._acknowledgeEventChannel).Select(x => x.LockToken);
                await this._eventGridTopicSubscription.Client.AcknowledgeCloudEventsAsync(this._eventGridTopicSubscription.TopicName, this._eventGridTopicSubscription.SubscriptionName, lockTokens, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task ReleaseEvents(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && !this._releaseEventChannel.Reader.Completion.IsCompleted)
            {
                if (!await this._releaseEventChannel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return;
                }

                var eventBundlesByDelay = ReadCurrentContent(this._releaseEventChannel)
                    .GroupBy(x => this.GetReleaseDelay(x.DeliveryCount));

                foreach (var eventBundle in eventBundlesByDelay)
                {
                    var lockTokens = eventBundle.Select(x => x.LockToken);
                    await this._eventGridTopicSubscription.Client.ReleaseCloudEventsAsync(this._eventGridTopicSubscription.TopicName, this._eventGridTopicSubscription.SubscriptionName, lockTokens, eventBundle.Key, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private async Task RejectEvents(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && !this._rejectEventChannel.Reader.Completion.IsCompleted)
            {
                if (!await this._rejectEventChannel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return;
                }

                var lockTokens = ReadCurrentContent(this._rejectEventChannel).Select(x => x.LockToken);
                await this._eventGridTopicSubscription.Client.RejectCloudEventsAsync(this._eventGridTopicSubscription.TopicName, this._eventGridTopicSubscription.SubscriptionName, lockTokens, cancellationToken).ConfigureAwait(false);
            }
        }

        private void CompleteEventChannels()
        {
            this._acknowledgeEventChannel.Writer.TryComplete();
            this._releaseEventChannel.Writer.TryComplete();
            this._rejectEventChannel.Writer.TryComplete();
        }

        private void ResetEventChannels()
        {
            this._acknowledgeEventChannel = Channel.CreateBounded<EventBundle>(OutputChannelSize);
            this._releaseEventChannel = Channel.CreateBounded<EventBundle>(OutputChannelSize);
            this._rejectEventChannel = Channel.CreateBounded<EventBundle>(OutputChannelSize);
        }

        private void SignalHandlerStarting()
        {
            Interlocked.Increment(ref this._handlersInProgress);
        }

        private void SignalHandlerCompleted()
        {
            Interlocked.Decrement(ref this._handlersInProgress);
            lock (this._eventGridTopicSubscription)
            {
                this._taskCompletionSource.TrySetResult();
                this._taskCompletionSource = new TaskCompletionSource();
            }
        }

        private ValueTask WaitForAvailableHandler(CancellationToken cancellationToken)
        {
            lock (this._eventGridTopicSubscription)
            {
                if (this._handlersInProgress < this._eventGridTopicSubscription.MaxHandlerDop)
                {
                    return ValueTask.CompletedTask;
                }

                return new ValueTask(this._taskCompletionSource.Task.WaitAsync(cancellationToken));
            }
        }

        private TimeSpan GetReleaseDelay(int deliveryCount)
        {
            var delay = this._retryDelays?.GetValueOrDefault(deliveryCount, this._defaultDelay!.Value) ?? TimeSpan.FromSeconds((int)Math.Min(Math.Pow(2, deliveryCount - 1), int.MaxValue));
            return SupportedReleaseDelays.FirstOrDefault(x => delay <= x, SupportedReleaseDelays.Last());
        }

        private static IEnumerable<EventBundle> ReadCurrentContent(Channel<EventBundle> channel)
        {
            var maxResultCount = Math.Min(channel.Reader.Count, MaxEventRequestSize);
            var resultCounter = 0;

            while (channel.Reader.TryRead(out var result))
            {
                yield return result;

                if (++resultCounter >= maxResultCount)
                {
                    break;
                }
            }
        }
    }

    private record EventGridTopicSubscription(string TopicName, string SubscriptionName, int MaxHandlerDop, int MaxRetriesCount, List<TimeSpan>? RetryDelays, IEventGridClientAdapter Client);
}