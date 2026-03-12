namespace Workleap.DomainEventPropagation.Subscription.Tests;

internal sealed class ThrowingSubscriptionBehavior : ISubscriptionDomainEventBehavior
{
    public Task HandleAsync(IDomainEventWrapper domainEventWrapper, IDomainEventSubscriptionContext subscriptionContext, SubscriptionDomainEventHandlerDelegate next, CancellationToken cancellationToken)
    {
        if (domainEventWrapper.DomainEventName == nameof(ThrowingDomainEvent))
        {
            throw new Exception("Error publishing event");
        }

        return next(domainEventWrapper, subscriptionContext, cancellationToken);
    }
}