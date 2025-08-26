namespace Workleap.DomainEventPropagation.Subscription.Tests;

[DomainEvent(nameof(ThrowingDomainEvent))]
public class ThrowingDomainEvent : IDomainEvent
{
}