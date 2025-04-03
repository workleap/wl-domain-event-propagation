using Azure.Messaging;
using Microsoft.Extensions.Logging;

namespace Workleap.DomainEventPropagation;

internal sealed class CloudEventHandler : BaseEventHandler, ICloudEventHandler
{
    private readonly DomainEventHandlerDelegate _pipeline;

    public CloudEventHandler(
        IServiceProvider serviceProvider,
        IDomainEventTypeRegistry domainEventTypeRegistry,
        IEnumerable<ISubscriptionDomainEventBehavior> domainEventBehaviors)
        : base(serviceProvider, domainEventTypeRegistry)
    {
        this._pipeline = domainEventBehaviors.Reverse().Aggregate((DomainEventHandlerDelegate)this.HandleDomainEventAsync, BuildPipeline);
    }

    public async Task HandleCloudEventAsync(CloudEvent cloudEvent, CancellationToken cancellationToken)
    {
        var domainEventWrapper = WrapCloudEvent(cloudEvent);
        if (this.GetDomainEventType(domainEventWrapper.DomainEventName) == null)
        {
            throw new DomainEventTypeNotRegisteredException(domainEventWrapper.DomainEventName);
        }

        await this._pipeline(domainEventWrapper, cancellationToken).ConfigureAwait(false);
    }

    private static DomainEventWrapper WrapCloudEvent(CloudEvent cloudEvent)
    {
        try
        {
            return new DomainEventWrapper(cloudEvent);
        }
        catch (Exception ex)
        {
            throw new CloudEventSerializationException(cloudEvent.Type, ex);
        }
    }

    private static DomainEventHandlerDelegate BuildPipeline(DomainEventHandlerDelegate next, ISubscriptionDomainEventBehavior behavior)
    {
        return (@event, cancellationToken) => behavior.HandleAsync(@event, next, cancellationToken);
    }

    private async Task HandleDomainEventAsync(
        DomainEventWrapper domainEventWrapper,
        CancellationToken cancellationToken)
    {
        var handler = this.BuildHandleDomainEventAsyncMethod(domainEventWrapper, cancellationToken);

        if (handler == null)
        {
            throw new DomainEventHandlerNotRegisteredException(domainEventWrapper.DomainEventName);
        }

        await handler().ConfigureAwait(false);
    }
}