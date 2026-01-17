using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;

namespace Workleap.DomainEventPropagation;

internal sealed class EventGridRequestHandler : IEventGridRequestHandler
{
    private readonly IDomainEventGridWebhookHandler _domainEventGridWebhookHandler;
    private readonly ISubscriptionEventGridWebhookHandler _subscriptionEventGridWebhookHandler;

    public EventGridRequestHandler(
        IDomainEventGridWebhookHandler domainEventGridWebhookHandler,
        ISubscriptionEventGridWebhookHandler subscriptionEventGridWebhookHandler)
    {
        this._domainEventGridWebhookHandler = domainEventGridWebhookHandler;
        this._subscriptionEventGridWebhookHandler = subscriptionEventGridWebhookHandler;
    }

    public async Task<EventGridRequestResult> HandleRequestAsync(EventGridEvent[] eventGridEvents, IDomainEventSubscriptionContext subscriptionContext, CancellationToken cancellationToken)
    {
        foreach (var eventGridEvent in eventGridEvents)
        {
            if (eventGridEvent.TryGetSystemEventData(out var systemEventData))
            {
                if (systemEventData is SubscriptionValidationEventData subscriptionValidationEventData)
                {
                    return this.ProcessSubscriptionEvent(subscriptionValidationEventData);
                }

                return new EventGridRequestResult(EventGridRequestType.Unsupported);
            }

            if (!string.IsNullOrEmpty(eventGridEvent.Topic))
            {
                await this.ProcessDomainEventAsync(eventGridEvent, subscriptionContext, cancellationToken).ConfigureAwait(false);
            }
        }

        return new EventGridRequestResult(EventGridRequestType.Event);
    }

    public async Task<EventGridRequestResult> HandleRequestAsync(CloudEvent[] cloudEvents, IDomainEventSubscriptionContext subscriptionContext, CancellationToken cancellationToken)
    {
        foreach (var cloudEvent in cloudEvents)
        {
            if (cloudEvent.TryGetSystemEventData(out var systemEventData))
            {
                if (systemEventData is SubscriptionValidationEventData subscriptionValidationEventData)
                {
                    return this.ProcessSubscriptionEvent(subscriptionValidationEventData);
                }

                return new EventGridRequestResult(EventGridRequestType.Unsupported);
            }

            if (!string.IsNullOrEmpty(cloudEvent.Source))
            {
                await this.ProcessDomainEventAsync(cloudEvent, subscriptionContext, cancellationToken).ConfigureAwait(false);
            }
        }

        return new EventGridRequestResult(EventGridRequestType.Event);
    }

    // Special event that an EventGrid custom topic sends upon creation of a push model subscription. 
    private EventGridRequestResult ProcessSubscriptionEvent(SubscriptionValidationEventData subscriptionValidationEventData)
    {
        var response = this._subscriptionEventGridWebhookHandler.HandleEventGridSubscriptionEvent(subscriptionValidationEventData);
        return new EventGridRequestResult(EventGridRequestType.Subscription, response);
    }

    // Events that our services send
    private async Task ProcessDomainEventAsync(EventGridEvent eventGridEvent, IDomainEventSubscriptionContext subscriptionContext, CancellationToken cancellationToken)
    {
        await this._domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(eventGridEvent, subscriptionContext, cancellationToken).ConfigureAwait(false);
    }

    private async Task ProcessDomainEventAsync(CloudEvent cloudEvent, IDomainEventSubscriptionContext subscriptionContext, CancellationToken cancellationToken)
    {
        await this._domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(cloudEvent, subscriptionContext, cancellationToken).ConfigureAwait(false);
    }
}