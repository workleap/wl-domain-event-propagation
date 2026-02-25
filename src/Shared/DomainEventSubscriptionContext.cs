namespace Workleap.DomainEventPropagation;

internal sealed class DomainEventSubscriptionContext : IDomainEventSubscriptionContext
{
    public int AttemptCount { get; init; }

    public int MaxAttempts { get; init; }
}