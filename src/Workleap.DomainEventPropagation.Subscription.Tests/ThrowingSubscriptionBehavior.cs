namespace Workleap.DomainEventPropagation.Subscription.Tests;

internal sealed class ThrowingSubscriptionBehavior : ISubscriptionDomainEventBehavior
{
    public Task HandleAsync(DomainEventWrapper domainEventWrapper, DomainEventHandlerDelegate next, CancellationToken cancellationToken)
    {
        if (domainEventWrapper.DomainEventName == nameof(ThrowingDomainEvent))
        {
            throw new Exception("Error publishing event");
        }

        return next(domainEventWrapper, cancellationToken);
    }
}