namespace Workleap.DomainEventPropagation.Publishing.Tests;

[DomainEvent("sample-event")]
public class SampleDomainEvent : IDomainEvent
{
    public string? Message { get; set; }
}