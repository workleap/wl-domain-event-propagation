namespace Workleap.DomainEventPropagation.Publishing.Tests;

internal sealed class ThrowingPublishingBehavior : IPublishingDomainEventBehavior
{
    public Task HandleAsync(DomainEventWrapperCollection domainEventWrappers, DomainEventsHandlerDelegate next, CancellationToken cancellationToken)
    {
        if (domainEventWrappers.DomainEventName == nameof(ThrowingDomainEvent))
        {
            throw new Exception("Error publishing event");
        }

        return next(domainEventWrappers, cancellationToken);
    }
}