using System.Collections;

namespace Workleap.DomainEventPropagation;

internal sealed class DomainEventWrapperCollection : IDomainEventWrapperCollection
{
    private readonly IDomainEventWrapper[] _domainEventWrappers;
    private readonly List<Action<IDomainEventMetadata>> _configureDomainEventMetadataActions;

    private DomainEventWrapperCollection(IEnumerable<IDomainEventWrapper> domainEventWrappers, Action<IDomainEventMetadata>? configureDomainEventMetadata, string domainEventName, EventSchema schema)
    {
        this._domainEventWrappers = domainEventWrappers.ToArray();
        this.DomainEventName = domainEventName;
        this.DomainSchema = schema;
        this._configureDomainEventMetadataActions = configureDomainEventMetadata != null ? [configureDomainEventMetadata] : [];
    }

    public int Count => this._domainEventWrappers.Length;

    public string DomainEventName { get; }

    public EventSchema DomainSchema { get; }

    public IEnumerable<Action<IDomainEventMetadata>> ConfigureDomainEventMetadataActions => this._configureDomainEventMetadataActions.AsReadOnly();

    public void AddConfigureDomainEventMetadataAction(Action<IDomainEventMetadata> configureDomainEventMetadata)
    {
        if (this.DomainSchema != EventSchema.CloudEvent)
        {
            throw new NotSupportedException("Domain event configuration is only supported for CloudEvents");
        }

        this._configureDomainEventMetadataActions.Add(configureDomainEventMetadata);
    }

    public static DomainEventWrapperCollection Create<T>(IEnumerable<T> domainEvents, Action<IDomainEventMetadata>? configureDomainEventMetadata)
        where T : IDomainEvent
    {
        var domainEventWrappers = domainEvents.Select(DomainEventWrapper.Wrap).ToArray();

        return new DomainEventWrapperCollection(domainEventWrappers, configureDomainEventMetadata, DomainEventNameCache.GetName<T>(), DomainEventSchemaCache.GetEventSchema<T>());
    }

    public IEnumerator<IDomainEventWrapper> GetEnumerator()
    {
        // See https://stackoverflow.com/questions/1272673/obtain-generic-enumerator-from-an-array
        return ((IEnumerable<IDomainEventWrapper>)this._domainEventWrappers).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
    }
}