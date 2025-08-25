namespace Workleap.DomainEventPropagation;

internal delegate Task DomainEventHandlerDelegate(DomainEventWrapper domainEventWrapper, CancellationToken cancellationToken);

internal interface ISubscriptionDomainEventBehavior
{
    Task HandleAsync(DomainEventWrapper domainEventWrapper, DomainEventHandlerDelegate next, CancellationToken cancellationToken);
}