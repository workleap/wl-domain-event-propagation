using Azure.Messaging;
using Microsoft.Extensions.Logging;

namespace Workleap.DomainEventPropagation;

internal sealed class CloudEventHandler : BaseEventHandler, ICloudEventHandler
{
    private readonly SubscriptionDomainEventHandlerDelegate _pipeline;

    public CloudEventHandler(
        IServiceProvider serviceProvider,
        IDomainEventTypeRegistry domainEventTypeRegistry,
        IEnumerable<ISubscriptionDomainEventBehavior> domainEventBehaviors)
        : base(serviceProvider, domainEventTypeRegistry)
    {
        this._pipeline = domainEventBehaviors.Reverse().Aggregate((SubscriptionDomainEventHandlerDelegate)this.HandleDomainEventAsync, BuildPipeline);
    }

    public async Task HandleCloudEventAsync(CloudEvent cloudEvent, IDomainEventSubscriptionContext subscriptionContext, CancellationToken cancellationToken)
    {
        var domainEventWrapper = WrapCloudEvent(cloudEvent);
        if (this.GetDomainEventType(domainEventWrapper.DomainEventName) == null)
        {
            throw new DomainEventTypeNotRegisteredException(domainEventWrapper.DomainEventName);
        }

        await this._pipeline(domainEventWrapper, subscriptionContext, cancellationToken).ConfigureAwait(false);
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

    private static SubscriptionDomainEventHandlerDelegate BuildPipeline(SubscriptionDomainEventHandlerDelegate next, ISubscriptionDomainEventBehavior behavior)
    {
        return (@event, context, cancellationToken) => behavior.HandleAsync(@event, context, next, cancellationToken);
    }

    private async Task HandleDomainEventAsync(
        IDomainEventWrapper domainEventWrapper,
        IDomainEventSubscriptionContext subscriptionContext,
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