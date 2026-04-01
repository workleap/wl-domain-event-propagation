using Azure.Messaging;
using Azure.Messaging.EventGrid;

namespace Workleap.DomainEventPropagation;

internal interface IEventGridRequestHandler
{
    Task<EventGridRequestResult> HandleRequestAsync(EventGridEvent[] eventGridEvents, IDomainEventSubscriptionContext subscriptionContext, CancellationToken cancellationToken);

    Task<EventGridRequestResult> HandleRequestAsync(CloudEvent[] cloudEvents, IDomainEventSubscriptionContext subscriptionContext, CancellationToken cancellationToken);
}