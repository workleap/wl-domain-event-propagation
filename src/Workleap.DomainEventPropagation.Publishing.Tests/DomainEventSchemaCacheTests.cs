namespace Workleap.DomainEventPropagation.Publishing.Tests;

public class DomainEventSchemaCacheTests
{
    [Fact]
    public void Given_EventGridEvent_When_RetrievingSchema_Then_ExpectedSchemaIsRetrieved()
    {
        // Arrange & Act
        var eventGridSchema = DomainEventSchemaCache.GetEventSchema<EventGridSampleDomainEvent>();

        // Assert
        Assert.Equal(EventSchema.EventGridEvent, eventGridSchema);
    }

    [Fact]
    public void Given_CloudEvent_When_RetrievingSchema_Then_ExpectedSchemaIsRetrieved()
    {
        // Arrange & Act
        var cloudEventSchema = DomainEventSchemaCache.GetEventSchema<CloudEventSampleDomainEvent>();

        // Assert
        Assert.Equal(EventSchema.CloudEvent, cloudEventSchema);
    }

    [DomainEvent("sample-eg-event")]
    private sealed class EventGridSampleDomainEvent : IDomainEvent;

    [DomainEvent("sample-cloud-event", EventSchema.CloudEvent)]
    private sealed record CloudEventSampleDomainEvent(string AttributeName) : IDomainEvent;
}