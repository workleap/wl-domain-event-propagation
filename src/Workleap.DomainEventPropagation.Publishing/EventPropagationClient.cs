using System.Collections.Concurrent;
using Azure;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.Namespaces;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;

namespace Workleap.DomainEventPropagation;

/// <summary>
/// https://github.com/Azure/azure-sdk-for-net/blob/master/sdk/eventgrid/Azure.Messaging.EventGrid/README.md
/// </summary>
internal sealed class EventPropagationClient : IEventPropagationClient, IDisposable
{
    private const int EventGridMaxEventsPerBatch = 1000;
    private const int MaxQueueSize = 100;
    private const string DomainEventDefaultVersion = "1.0";

    private readonly TimeSpan _flushInterval = TimeSpan.FromMilliseconds(200);
    private readonly CancellationTokenSource _cts = new();
    private readonly Task? _backgroundTask;
    private readonly ConcurrentQueue<IDomainEvent> _domainEventQueue = new();

    private readonly EventPropagationPublisherOptions _eventPropagationPublisherOptions;
    private readonly DomainEventsHandlerDelegate _pipeline;
    private readonly EventGridPublisherClient? _eventGridPublisherClient;
    private readonly EventGridSenderClient? _eventGridNamespaceClient;

    /// <summary>
    /// To support Namespace topic, we need to use the following EventGridClient https://github.com/Azure/azure-sdk-for-net/blob/Azure.Messaging.EventGrid_4.17.0-beta.1/sdk/eventgrid/Azure.Messaging.EventGridV2/src/Generated/EventGridClient.cs
    /// Note that even if this client has the same name, it is different from the deprecated one found here https://learn.microsoft.com/en-us/dotnet/api/microsoft.azure.eventgrid.eventgridclient?view=azure-dotnet-legacy
    /// </summary>
    public EventPropagationClient(
        IAzureClientFactory<EventGridPublisherClient> eventGridPublisherClientFactory,
        IAzureClientFactory<EventGridSenderClient> eventGridClientFactory,
        IOptions<EventPropagationPublisherOptions> eventPropagationPublisherOptions,
        IEnumerable<IPublishingDomainEventBehavior> publishingDomainEventBehaviors)
    {
        this._eventPropagationPublisherOptions = eventPropagationPublisherOptions.Value;
        this._pipeline = publishingDomainEventBehaviors.Reverse().Aggregate((DomainEventsHandlerDelegate)this.SendDomainEventsAsync, BuildPipeline);

        switch (this._eventPropagationPublisherOptions.TopicType)
        {
            case TopicType.Custom:
                this._eventGridPublisherClient = eventGridPublisherClientFactory.CreateClient(EventPropagationPublisherOptions.EventGridClientName);
                this._backgroundTask = Task.Run(this.ProcessQueueAsync);
                break;
            case TopicType.Namespace:
                this._eventGridNamespaceClient = eventGridClientFactory.CreateClient(EventPropagationPublisherOptions.EventGridClientName);
                break;
            default:
                throw new InvalidOperationException($"Could not create the proper event grid client for topic type {this._eventPropagationPublisherOptions.TopicType}");
        }
    }

    private static DomainEventsHandlerDelegate BuildPipeline(DomainEventsHandlerDelegate accumulator, IPublishingDomainEventBehavior next)
    {
        return (events, cancellationToken) => next.HandleAsync(events, accumulator, cancellationToken);
    }

    public Task PublishDomainEventAsync<T>(T domainEvent, CancellationToken cancellationToken)
        where T : IDomainEvent
        => this.InternalPublishDomainEventsAsync(new[] { domainEvent }, null, cancellationToken);

    public void PublishAndQueueDomainEvent(IDomainEvent domainEvent) => this._domainEventQueue.Enqueue(domainEvent);

    public Task PublishDomainEventAsync<T>(T domainEvent, Action<IDomainEventMetadata> configureDomainEventMetadata, CancellationToken cancellationToken)
        where T : IDomainEvent
        => this.InternalPublishDomainEventsAsync(new[] { domainEvent }, configureDomainEventMetadata, cancellationToken);

    public Task PublishDomainEventsAsync<T>(IEnumerable<T> domainEvents, CancellationToken cancellationToken)
        where T : IDomainEvent
        => this.InternalPublishDomainEventsAsync(domainEvents, null, cancellationToken);

    public Task PublishDomainEventsAsync<T>(IEnumerable<T> domainEvents, Action<IDomainEventMetadata> configureDomainEventMetadata, CancellationToken cancellationToken)
        where T : IDomainEvent
        => this.InternalPublishDomainEventsAsync(domainEvents, configureDomainEventMetadata, cancellationToken);

    public void Dispose()
    {
        this._cts.Cancel();
        this._backgroundTask?.Wait();
    }

    private async Task InternalPublishDomainEventsAsync<T>(IEnumerable<T> domainEvents, Action<IDomainEventMetadata>? configureDomainEventMetadata, CancellationToken cancellationToken)
        where T : IDomainEvent
    {
        if (domainEvents == null)
        {
            throw new ArgumentNullException(nameof(domainEvents));
        }

        var domainEventWrappers = DomainEventWrapperCollection.Create(domainEvents, configureDomainEventMetadata);
        if (domainEventWrappers.Count == 0)
        {
            return;
        }

        if (configureDomainEventMetadata != null && domainEventWrappers.DomainSchema != EventSchema.CloudEvent)
        {
            throw new NotSupportedException("Domain event configuration is only supported for CloudEvents");
        }

        try
        {
            await this._pipeline(domainEventWrappers, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new EventPropagationPublishingException(domainEventWrappers.DomainEventName, this._eventPropagationPublisherOptions.TopicEndpoint, ex);
        }
    }

    private Task SendDomainEventsAsync(DomainEventWrapperCollection domainEventWrappers, CancellationToken cancellationToken)
    {
        return domainEventWrappers.DomainSchema switch
        {
            EventSchema.EventGridEvent => this.SendEventGridEvents(domainEventWrappers, cancellationToken),
            EventSchema.CloudEvent => this.SendCloudEvents(domainEventWrappers, cancellationToken),
            _ => throw new NotSupportedException($"Event schema {domainEventWrappers.DomainSchema} is not supported"),
        };
    }

    private async Task SendEventGridEvents(
        DomainEventWrapperCollection domainEventWrappers,
        CancellationToken cancellationToken)
    {
        if (this._eventGridPublisherClient == null)
        {
            throw new InvalidOperationException($"Unable to send eventGrid event because the {nameof(EventGridPublisherClient)} is null");
        }

        var topicType = this._eventPropagationPublisherOptions.TopicType;
        var eventGridEvents = domainEventWrappers.Select(wrapper => new EventGridEvent(
            subject: wrapper.DomainEventName,
            eventType: wrapper.DomainEventName,
            dataVersion: DomainEventDefaultVersion,
            data: new BinaryData(wrapper.Data)));

        switch (topicType)
        {
            case TopicType.Custom:
                break;
            case TopicType.Namespace:
                throw new NotSupportedException("Cannot send EventGridEvents to a namespace topic");
            default:
                throw new NotSupportedException($"Topic type {topicType} is not supported");
        }

        foreach (var eventBatch in Chunk(eventGridEvents, EventGridMaxEventsPerBatch))
        {
            await this._eventGridPublisherClient.SendEventsAsync(eventBatch, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendCloudEvents(
        DomainEventWrapperCollection domainEventWrappers,
        CancellationToken cancellationToken)
    {
        var cloudEvents = domainEventWrappers.Select(wrapper => new CloudEvent(
            type: wrapper.DomainEventName,
            source: this._eventPropagationPublisherOptions.TopicEndpoint,
            jsonSerializableData: wrapper.Data));

        if (domainEventWrappers.ConfigureDomainEventMetadata != null)
        {
            cloudEvents = cloudEvents.Select(cloudEvent =>
            {
                domainEventWrappers.ConfigureDomainEventMetadata(new DomainEventMetadataWrapper(cloudEvent));
                return cloudEvent;
            });
        }

        foreach (var eventBatch in Chunk(cloudEvents, EventGridMaxEventsPerBatch))
        {
            if (this._eventGridPublisherClient != null)
            {
                await this._eventGridPublisherClient.SendEventsAsync(eventBatch, cancellationToken).ConfigureAwait(false);
            }
            else if (this._eventGridNamespaceClient != null)
            {
                await this._eventGridNamespaceClient.SendAsync(eventBatch, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static IEnumerable<List<T>> Chunk<T>(IEnumerable<T> source, int chunkSize)
    {
        var chunk = new List<T>(chunkSize);

        foreach (var item in source)
        {
            chunk.Add(item);
            if (chunk.Count == chunkSize)
            {
                yield return chunk;
                chunk = new List<T>(chunkSize);
            }
        }

        if (chunk.Any())
        {
            yield return chunk;
        }
    }

    private async Task ProcessQueueAsync()
    {
        while (!this._cts.Token.IsCancellationRequested)
        {
            await Task.Delay(this._flushInterval).ConfigureAwait(false);

            var eventsToSend = new List<IDomainEvent>();
            while (eventsToSend.Count < MaxQueueSize && this._domainEventQueue.TryDequeue(out var evt))
            {
                eventsToSend.Add(evt);
            }

            if (eventsToSend.Count > 0)
            {
                await this.InternalPublishDomainEventsAsync(eventsToSend, null, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}

