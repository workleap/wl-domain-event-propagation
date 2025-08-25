using Officevibe.DomainEvents;

namespace Workleap.DomainEventPropagation.Subscription.Tests;

public class DomainEventTypeRegistryTests
{
    private readonly DomainEventTypeRegistry _registry = new();

    [Fact]
    public void GivenRegularDomainEventType_WhenRegisterDomainEvent_ThenHandlerAndEventRegisteredOnce()
    {
        // Given
        var domainEventType = typeof(SampleDomainEvent);

        // When
        this._registry.RegisterDomainEvent(domainEventType);

        var eventType = this._registry.GetDomainEventType("sample-event");
        var officevibeEventType = this._registry.GetDomainEventType(domainEventType.FullName!);

        var handlerType = this._registry.GetDomainEventHandlerType("sample-event");
        var officevibeHandlerType = this._registry.GetDomainEventHandlerType(domainEventType.FullName!);

        // Then
        Assert.NotNull(eventType);
        Assert.Null(officevibeEventType);

        Assert.NotNull(handlerType);
        Assert.Null(officevibeHandlerType);
    }

    [Fact]
    public void GivenOfficevibeDomainEventType_WhenRegisterDomainEvent_ThenHandlerAndEventRegisteredTwice()
    {
        // Given
        var domainEventType = typeof(OfficevibeEvent);

        // When
        this._registry.RegisterDomainEvent(domainEventType);

        var eventType = this._registry.GetDomainEventType("officevibe-event");
        var officevibeEventType = this._registry.GetDomainEventType(domainEventType.FullName!);

        var handlerType = this._registry.GetDomainEventHandlerType("officevibe-event");
        var officevibeHandlerType = this._registry.GetDomainEventHandlerType(domainEventType.FullName!);

        // Then
        Assert.NotNull(eventType);
        Assert.NotNull(officevibeEventType);

        Assert.NotNull(handlerType);
        Assert.NotNull(officevibeHandlerType);
    }

    [Fact]
    public void GivenTwoEventsWithTheSameName_WhenRegisterDomainEvent_ThenThrows()
    {
        // Given
        var eventType = typeof(SampleDomainEvent);
        var eventTypeCopy = typeof(SampleDomainEventCopy);

        // When
        this._registry.RegisterDomainEvent(eventType);

        // Then
        Assert.Throws<ArgumentException>(() => this._registry.RegisterDomainEvent(eventTypeCopy));
    }

    [DomainEvent("sample-event")]
    private sealed class SampleDomainEventCopy : IDomainEvent
    {
    }
}