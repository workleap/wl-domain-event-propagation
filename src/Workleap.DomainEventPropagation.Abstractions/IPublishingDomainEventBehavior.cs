namespace Workleap.DomainEventPropagation;

public delegate Task PublishingDomainEventHandlerDelegate(IDomainEventWrapperCollection domainEventWrappers, CancellationToken cancellationToken);

public interface IPublishingDomainEventBehavior
{
    Task HandleAsync(IDomainEventWrapperCollection domainEventWrappers, PublishingDomainEventHandlerDelegate next, CancellationToken cancellationToken);
}
