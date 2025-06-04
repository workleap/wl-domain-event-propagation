namespace Workleap.DomainEventPropagation;

public class EventPropagationThrottlingOptions
{
    internal const string DefaultSectionName = "EventPropagation:ThrottlingOptions";

    public int MaxEventsPerSecond { get; set; }

    public int QueueLimit { get; set; }
}