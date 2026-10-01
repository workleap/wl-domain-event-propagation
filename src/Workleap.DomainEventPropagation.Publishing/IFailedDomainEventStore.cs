namespace Workleap.DomainEventPropagation;

/// <summary>
/// Persists domain events that could not be published so they can be republished later with <see cref="IFailedDomainEventRepublisher"/>.
/// Register an implementation with <see cref="EventPropagationPublisherBuilderFailedDomainEventStoreExtensions.AddFailedDomainEventStore{TStore}"/>.
/// The store is a singleton.
/// </summary>
public interface IFailedDomainEventStore
{
    /// <summary>
    /// Called when publishing fails.
    /// Return <c>true</c> once the events are durably stored: the publish call then completes without throwing.
    /// Return <c>false</c> to decline (for example when a feature flag is off): the publish call throws <see cref="EventPropagationPublishingException"/> as it would without a store.
    /// If this method throws, the publish call throws <see cref="EventPropagationPublishingException"/> too.
    /// </summary>
    /// <param name="domainEvents">The events of the failed publish call. They all share the same name and schema.</param>
    /// <param name="exception">The publishing failure.</param>
    /// <param name="cancellationToken">
    /// Not linked to the publish call's token: the store must still run when the caller was cancelled,
    /// because the caller's own changes are usually already committed.
    /// </param>
    Task<bool> TryStoreAsync(IReadOnlyCollection<FailedDomainEvent> domainEvents, EventPropagationPublishingException exception, CancellationToken cancellationToken);
}