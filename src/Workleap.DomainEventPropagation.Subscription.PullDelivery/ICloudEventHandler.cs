using Azure.Messaging;

namespace Workleap.DomainEventPropagation;

internal interface ICloudEventHandler
{
    Task HandleCloudEventAsync(CloudEvent cloudEvent, CancellationToken cancellationToken);
}