namespace Workleap.DomainEventPropagation.Publishing.Tests;

[DomainEvent(nameof(ThrowingDomainEvent))]
public class ThrowingDomainEvent : IDomainEvent
{
}