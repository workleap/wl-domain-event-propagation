using System.Collections.Concurrent;

namespace Workleap.DomainEventPropagation.Subscription.PullDelivery.Tests.Events;

[DomainEvent(Constants.SampleEventName, EventSchema.CloudEvent)]
public class SampleEvent : IDomainEvent
{
    public string? Message { get; set; }
}

public class SampleEventTestHandler : IDomainEventHandler<SampleEvent>
{
    public static ConcurrentQueue<SampleEvent> ReceivedEvents { get; } = new();

    public Task HandleDomainEventAsync(SampleEvent domainEvent, CancellationToken cancellationToken)
    {
        ReceivedEvents.Enqueue(domainEvent);
        return Task.CompletedTask;
    }
}