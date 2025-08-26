namespace Workleap.DomainEventPropagation;

internal delegate Task DomainEventsHandlerDelegate(DomainEventWrapperCollection domainEventWrappers, CancellationToken cancellationToken);

internal interface IPublishingDomainEventBehavior
{
    Task HandleAsync(DomainEventWrapperCollection domainEventWrappers, DomainEventsHandlerDelegate next, CancellationToken cancellationToken);
}