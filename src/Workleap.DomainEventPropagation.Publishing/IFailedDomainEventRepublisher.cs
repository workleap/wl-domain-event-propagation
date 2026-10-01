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
    Task RepublishAsync(FailedDomainEvent domainEvent, CancellationToken cancellationToken);
}