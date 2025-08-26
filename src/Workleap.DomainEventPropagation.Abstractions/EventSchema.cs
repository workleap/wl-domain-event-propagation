namespace Workleap.DomainEventPropagation;

public enum EventSchema
{
    None = 0,
    EventGridEvent = 1,
    CloudEvent = 2,
}