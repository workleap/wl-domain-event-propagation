using System.Reflection;
using System.Text.Json;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Microsoft.Extensions.Logging;

namespace Workleap.DomainEventPropagation;

internal sealed class DomainEventGridWebhookHandler : BaseEventHandler, IDomainEventGridWebhookHandler
{
    private readonly ILogger<DomainEventGridWebhookHandler> _logger;
    private readonly SubscriptionDomainEventHandlerDelegate _pipeline;

    public DomainEventGridWebhookHandler(
        IServiceProvider serviceProvider,
        IDomainEventTypeRegistry domainEventTypeRegistry,
        ILogger<DomainEventGridWebhookHandler> logger,
        IEnumerable<ISubscriptionDomainEventBehavior> subscriptionDomainEventBehaviors)
        : base(serviceProvider, domainEventTypeRegistry)
    {
        this._logger = logger;
        this._pipeline = subscriptionDomainEventBehaviors.Reverse().Aggregate((SubscriptionDomainEventHandlerDelegate)this.HandleDomainEventAsync, BuildPipeline);
    }

    private static SubscriptionDomainEventHandlerDelegate BuildPipeline(SubscriptionDomainEventHandlerDelegate next, ISubscriptionDomainEventBehavior pipeline)
    {
        return (events, context, cancellationToken) => pipeline.HandleAsync(events, context, next, cancellationToken);
    }

    public async Task HandleEventGridWebhookEventAsync(EventGridEvent eventGridEvent, IDomainEventSubscriptionContext subscriptionContext, CancellationToken cancellationToken)
    {
        // Check if it's an old Officevibe event
        if (eventGridEvent.EventType.StartsWith("Officevibe", StringComparison.Ordinal))
        {
            var domainEventType = this.GetDomainEventType(eventGridEvent.EventType);
            if (domainEventType != null)
            {
                await this.HandleOfficevibeDomainEventAsync(eventGridEvent, domainEventType, cancellationToken).ConfigureAwait(false);
                return;
            }

            this._logger.EventDomainTypeNotRegistered(eventGridEvent.EventType, eventGridEvent.Subject);
            return;
        }

        var domainEventWrapper = new DomainEventWrapper(eventGridEvent);

        if (this.GetDomainEventType(domainEventWrapper.DomainEventName) == null)
        {
            this._logger.EventDomainTypeNotRegistered(domainEventWrapper.DomainEventName, eventGridEvent.Subject);
            return;
        }

        await this._pipeline(domainEventWrapper, subscriptionContext, cancellationToken).ConfigureAwait(false);
    }

    public async Task HandleEventGridWebhookEventAsync(CloudEvent cloudEvent, IDomainEventSubscriptionContext subscriptionContext, CancellationToken cancellationToken)
    {
        var domainEventWrapper = new DomainEventWrapper(cloudEvent);

        if (this.GetDomainEventType(domainEventWrapper.DomainEventName) == null)
        {
            this._logger.EventDomainTypeNotRegistered(domainEventWrapper.DomainEventName, cloudEvent.Source);
            return;
        }

        await this._pipeline(domainEventWrapper, subscriptionContext, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleDomainEventAsync(IDomainEventWrapper domainEventWrapper, IDomainEventSubscriptionContext subscriptionContext, CancellationToken cancellationToken)
    {
        var handler = this.BuildHandleDomainEventAsyncMethod(domainEventWrapper, cancellationToken);
        if (handler == null)
        {
            this._logger.EventDomainHandlerNotRegistered(domainEventWrapper.DomainEventName);
            return;
        }

        await handler().ConfigureAwait(false);
    }

    private async Task HandleOfficevibeDomainEventAsync(EventGridEvent eventGridEvent, Type domainEventType, CancellationToken cancellationToken)
    {
        var domainEventHandlerType = this.GetDomainEventHandlerType(domainEventType.FullName!)!;
        var domainEventHandler = this.ResolveDomainEventHandler(domainEventHandlerType);

        if (domainEventHandler == null)
        {
            this._logger.EventDomainHandlerNotRegistered(eventGridEvent.EventType);
            return;
        }

        var domainEvent = JsonSerializer.Deserialize(eventGridEvent.Data, domainEventType, JsonSerializerConstants.DomainEventSerializerOptions);
        var domainEventHandlerMethod = GetHandleDomainEventAsyncMethod(domainEventHandlerType);

        await ((Task)domainEventHandlerMethod.Invoke(domainEventHandler, new[] { domainEvent, cancellationToken })!).ConfigureAwait(false);
    }

    private static MethodInfo GetHandleDomainEventAsyncMethod(Type domainEventHandlerType)
    {
        return GenericDomainEventHandlerMethodCache.GetOrAdd(domainEventHandlerType, type =>
        {
            const string handleDomainEventAsyncMethodName = "HandleDomainEventAsync";
            return type.GetMethod(handleDomainEventAsyncMethodName, BindingFlags.Public | BindingFlags.Instance) ??
                   throw new InvalidOperationException($"Public method {type.FullName}.{handleDomainEventAsyncMethodName} not found");
        });
    }
}