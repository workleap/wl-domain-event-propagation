using System.Collections;

namespace Workleap.DomainEventPropagation;

internal sealed class DomainEventWrapperCollection : IReadOnlyCollection<DomainEventWrapper>
{
    private readonly DomainEventWrapper[] _domainEventWrappers;

    private DomainEventWrapperCollection(IEnumerable<DomainEventWrapper> domainEventWrappers, Action<IDomainEventMetadata>? configureDomainEventMetadata, string domainEventName, EventSchema schema)
    {
        this._domainEventWrappers = domainEventWrappers.ToArray();
        this.DomainEventName = domainEventName;
        this.DomainSchema = schema;
        this.ConfigureDomainEventMetadata = configureDomainEventMetadata;
    }

    public int Count => this._domainEventWrappers.Length;

    public string DomainEventName { get; }

    public EventSchema DomainSchema { get; }

    public Action<IDomainEventMetadata>? ConfigureDomainEventMetadata { get; }

    public static DomainEventWrapperCollection Create<T>(IEnumerable<T> domainEvents, Action<IDomainEventMetadata>? configureDomainEventMetadata)
        where T : IDomainEvent
    {
        var domainEventWrappers = domainEvents.Select(DomainEventWrapper.Wrap).ToArray();

        return new DomainEventWrapperCollection(domainEventWrappers, configureDomainEventMetadata, DomainEventNameCache.GetName<T>(), DomainEventSchemaCache.GetEventSchema<T>());
    }

    public static DomainEventWrapperCollection Create(DomainEventWrapper domainEventWrapper)
    {
        return new DomainEventWrapperCollection(new[] { domainEventWrapper }, configureDomainEventMetadata: null, domainEventWrapper.DomainEventName, domainEventWrapper.DomainEventSchema);
    }

    public IEnumerator<DomainEventWrapper> GetEnumerator()
    {
        // See https://stackoverflow.com/questions/1272673/obtain-generic-enumerator-from-an-array
        return ((IEnumerable<DomainEventWrapper>)this._domainEventWrappers).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
    }
}