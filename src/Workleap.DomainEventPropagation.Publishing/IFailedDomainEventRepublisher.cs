namespace Workleap.DomainEventPropagation;

/// <summary>
/// Publishes a domain event previously handed to <see cref="IFailedDomainEventStore"/>.
/// </summary>
public interface IFailedDomainEventRepublisher
{
    /// <summary>
    /// Publishes the event through the same pipeline as <see cref="IEventPropagationClient"/>.
    /// Throws <see cref="EventPropagationPublishingException"/> on failure and never hands the event to <see cref="IFailedDomainEventStore"/> again.
    /// </summary>
    /// <exception cref="EventPropagationPublishingException">Publishing failed. The event can be retried later.</exception>
    /// <exception cref="ArgumentException">
    /// <see cref="FailedDomainEvent.Data"/> is not a JSON object, for example because the stored value is corrupted.
    /// Retrying will not succeed, so stop republishing this event.
    /// </exception>
    Task RepublishAsync(FailedDomainEvent domainEvent, CancellationToken cancellationToken);
}