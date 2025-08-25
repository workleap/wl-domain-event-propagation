namespace Workleap.DomainEventPropagation.Subscription.Tests;

[DomainEvent("sample-event")]
public sealed record SampleDomainEvent(string Message) : IDomainEvent;

public class SampleDomainEventHandler : IDomainEventHandler<SampleDomainEvent>
{
    public Task HandleDomainEventAsync(SampleDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}