namespace Workleap.DomainEventPropagation;

public interface IDomainEventContext
{
    string? Id { get; }

    string? Source { get; }

    string DomainEventName { get; }

    EventSchema EventSchema { get; }
}