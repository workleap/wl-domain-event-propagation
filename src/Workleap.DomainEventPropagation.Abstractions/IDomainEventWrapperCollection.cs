namespace Workleap.DomainEventPropagation;

public interface IDomainEventWrapperCollection : IReadOnlyCollection<IDomainEventWrapper>
{
    string DomainEventName { get; }

    EventSchema DomainSchema { get; }

    IEnumerable<Action<IDomainEventMetadata>> ConfigureDomainEventMetadataActions { get; }

    void AddConfigureDomainEventMetadataAction(Action<IDomainEventMetadata> configureDomainEventMetadata);
}
