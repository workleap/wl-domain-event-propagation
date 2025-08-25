namespace Workleap.DomainEventPropagation.Subscription.PullDelivery.Tests.Events;

[DomainEvent("sample-event-that-causes-exception", EventSchema.CloudEvent)]
public class SampleThatCausesExceptionDomainEvent : IDomainEvent
{
    public string? Message { get; set; }
}

public class SampleThatCausesExceptionDomainEventHandler : IDomainEventHandler<SampleThatCausesExceptionDomainEvent>
{
    public Task HandleDomainEventAsync(SampleThatCausesExceptionDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        throw new Exception();
    }
}