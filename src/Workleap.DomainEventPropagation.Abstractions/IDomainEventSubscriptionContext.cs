namespace Workleap.DomainEventPropagation;

public interface IDomainEventSubscriptionContext
{
    int AttemptCount { get; }

    int MaxAttempts { get; }
}
