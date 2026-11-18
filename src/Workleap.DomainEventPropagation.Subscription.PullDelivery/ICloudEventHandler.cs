using Azure.Messaging;

namespace Workleap.DomainEventPropagation;

internal interface ICloudEventHandler
{
    Task HandleCloudEventAsync(CloudEvent cloudEvent, int deliveryCount, bool isLastDelivery, CancellationToken cancellationToken);
}