namespace Workleap.DomainEventPropagation;

/// <summary>
/// A domain event that could not be published, in the serialized form it was sent with.
/// Persist these three values as-is and pass them back to <see cref="IFailedDomainEventRepublisher"/> to publish the event again.
/// </summary>
public sealed class FailedDomainEvent
{
    public FailedDomainEvent(string domainEventName, EventSchema schema, string data)
    {
        this.DomainEventName = domainEventName ?? throw new ArgumentNullException(nameof(domainEventName));
        this.Schema = schema;
        this.Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public string DomainEventName { get; }

    public EventSchema Schema { get; }

    /// <summary>
    /// The domain event serialized as a JSON object, including the tracing metadata injected at publish time.
    /// </summary>
    public string Data { get; }
}