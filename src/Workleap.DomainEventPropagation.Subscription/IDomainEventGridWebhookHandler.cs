using Azure.Messaging;
using Azure.Messaging.EventGrid;

namespace Workleap.DomainEventPropagation;

internal interface IDomainEventGridWebhookHandler
{
    Task HandleEventGridWebhookEventAsync(EventGridEvent eventGridEvent, IDomainEventSubscriptionContext subscriptionContext, CancellationToken cancellationToken);

    Task HandleEventGridWebhookEventAsync(CloudEvent cloudEvent, IDomainEventSubscriptionContext subscriptionContext, CancellationToken cancellationToken);
}