namespace Workleap.DomainEventPropagation;

public interface IEventPropagationClient
{
    Task PublishDomainEventAsync<T>(T domainEvent, CancellationToken cancellationToken)
        where T : IDomainEvent;

    Task PublishDomainEventAsync<T>(T domainEvent, Action<IDomainEventMetadata> configureDomainEventMetadata, CancellationToken cancellationToken)
        where T : IDomainEvent;

    Task PublishDomainEventsAsync<T>(IEnumerable<T> domainEvents, CancellationToken cancellationToken)
        where T : IDomainEvent;

    Task PublishDomainEventsAsync<T>(IEnumerable<T> domainEvents, Action<IDomainEventMetadata> configureDomainEventMetadata, CancellationToken cancellationToken)
        where T : IDomainEvent;
}