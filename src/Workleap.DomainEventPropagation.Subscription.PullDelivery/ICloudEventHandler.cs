using Azure.Messaging;

namespace Workleap.DomainEventPropagation;

public interface ICloudEventHandler
{
    Task HandleCloudEventAsync(CloudEvent cloudEvent, IDomainEventSubscriptionContext subscriptionContext, CancellationToken cancellationToken);
}