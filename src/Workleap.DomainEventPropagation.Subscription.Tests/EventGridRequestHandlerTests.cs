using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using FakeItEasy;

namespace Workleap.DomainEventPropagation.Subscription.Tests;

public class EventGridRequestHandlerTests
{
    private readonly IDomainEventSubscriptionContext _subscriptionContext = A.Fake<IDomainEventSubscriptionContext>();

    [Fact]
    public async Task GivenSubscriptionEventGridRequest_WhenRequestContentValid_ThenAcceptResponseIsGenerated()
    {
        // Given
        var validationCode = Guid.NewGuid().ToString();
        var domainEventGridWebhookHandler = A.Fake<IDomainEventGridWebhookHandler>();
        var subscriptionEventGridWebhookHandler = A.Fake<ISubscriptionEventGridWebhookHandler>();

        A.CallTo(() => subscriptionEventGridWebhookHandler.HandleEventGridSubscriptionEvent(A<SubscriptionValidationEventData>._))
            .Returns(new SubscriptionValidationResponse { ValidationResponse = validationCode });

        // When
        var eventGridRequestHandler = new EventGridRequestHandler(
            domainEventGridWebhookHandler,
            subscriptionEventGridWebhookHandler);

        var request = GetEventGridSubscriptionRequest(validationCode);

        var result = await eventGridRequestHandler.HandleRequestAsync(request, this._subscriptionContext, CancellationToken.None);

        // Then
        Assert.NotNull(result);
        Assert.NotNull(result.ValidationResponse);
        Assert.Equal(validationCode, result.ValidationResponse.ValidationResponse);
        Assert.Equal(EventGridRequestType.Subscription, result.RequestType);
    }

    [Fact]
    public async Task GivenSubscriptionCloudEventRequest_WhenRequestContentValid_ThenAcceptResponseIsGenerated()
    {
        // Given
        var validationCode = Guid.NewGuid().ToString();
        var domainEventGridWebhookHandler = A.Fake<IDomainEventGridWebhookHandler>();
        var subscriptionEventGridWebhookHandler = A.Fake<ISubscriptionEventGridWebhookHandler>();

        A.CallTo(() => subscriptionEventGridWebhookHandler.HandleEventGridSubscriptionEvent(A<SubscriptionValidationEventData>._))
            .Returns(new SubscriptionValidationResponse { ValidationResponse = validationCode });

        // When
        var eventGridRequestHandler = new EventGridRequestHandler(
            domainEventGridWebhookHandler,
            subscriptionEventGridWebhookHandler);

        var request = GetCloudEventSubscriptionRequest(validationCode);

        var result = await eventGridRequestHandler.HandleRequestAsync(request, this._subscriptionContext, CancellationToken.None);

        // Then
        Assert.NotNull(result);
        Assert.NotNull(result.ValidationResponse);
        Assert.Equal(validationCode, result.ValidationResponse.ValidationResponse);
        Assert.Equal(EventGridRequestType.Subscription, result.RequestType);
    }

    [Fact]
    public async Task GivenSubscriptionEventGridRequest_WhenExceptionIsThrown_ThenExceptionIsTracked()
    {
        // Given
        var validationCode = Guid.NewGuid().ToString();
        var domainEventGridWebhookHandler = A.Fake<IDomainEventGridWebhookHandler>();
        var subscriptionEventGridWebhookHandler = A.Fake<ISubscriptionEventGridWebhookHandler>();

        A.CallTo(() => subscriptionEventGridWebhookHandler.HandleEventGridSubscriptionEvent(A<SubscriptionValidationEventData>._))
            .Throws(new Exception("An exception was thrown"));

        // When
        var eventGridRequestHandler = new EventGridRequestHandler(
            domainEventGridWebhookHandler,
            subscriptionEventGridWebhookHandler);

        var request = GetEventGridSubscriptionRequest(validationCode);

        var exception = await Assert.ThrowsAsync<Exception>(() =>
        {
            return eventGridRequestHandler.HandleRequestAsync(request, this._subscriptionContext, CancellationToken.None);
        });

        // Then
        Assert.NotNull(exception);
    }

    [Fact]
    public async Task GivenSubscriptionCloudEventRequest_WhenExceptionIsThrown_ThenExceptionIsTracked()
    {
        // Given
        var validationCode = Guid.NewGuid().ToString();
        var domainEventGridWebhookHandler = A.Fake<IDomainEventGridWebhookHandler>();
        var subscriptionEventGridWebhookHandler = A.Fake<ISubscriptionEventGridWebhookHandler>();

        A.CallTo(() => subscriptionEventGridWebhookHandler.HandleEventGridSubscriptionEvent(A<SubscriptionValidationEventData>._))
            .Throws(new Exception("An exception was thrown"));

        // When
        var eventGridRequestHandler = new EventGridRequestHandler(
            domainEventGridWebhookHandler,
            subscriptionEventGridWebhookHandler);

        var request = GetCloudEventSubscriptionRequest(validationCode);

        var exception = await Assert.ThrowsAsync<Exception>(() =>
        {
            return eventGridRequestHandler.HandleRequestAsync(request, this._subscriptionContext, CancellationToken.None);
        });

        // Then
        Assert.NotNull(exception);
    }

    [Fact]
    public async Task GivenSubscriptionEventGridRequest_WhenRequestContentValidAndContainsTelemetryCorrelationId_ThenRequestTelemetryParentIdIsNotSet()
    {
        // Given
        var validationCode = Guid.NewGuid().ToString();
        var domainEventGridWebhookHandler = A.Fake<IDomainEventGridWebhookHandler>();
        var subscriptionEventGridWebhookHandler = A.Fake<ISubscriptionEventGridWebhookHandler>();

        A.CallTo(() => subscriptionEventGridWebhookHandler.HandleEventGridSubscriptionEvent(A<SubscriptionValidationEventData>._))
            .Returns(new SubscriptionValidationResponse { ValidationResponse = validationCode });

        // When
        var eventGridRequestHandler = new EventGridRequestHandler(
            domainEventGridWebhookHandler,
            subscriptionEventGridWebhookHandler);

        var request = GetEventGridSubscriptionRequest(validationCode);
        var result = await eventGridRequestHandler.HandleRequestAsync(request, this._subscriptionContext, CancellationToken.None);

        // Then
        Assert.NotNull(result);
        Assert.NotNull(result.ValidationResponse);
        Assert.Equal(validationCode, result.ValidationResponse.ValidationResponse);
        Assert.Equal(EventGridRequestType.Subscription, result.RequestType);
    }

    [Fact]
    public async Task GivenSubscriptionCloudEventRequest_WhenRequestContentValidAndContainsTelemetryCorrelationId_ThenRequestTelemetryParentIdIsNotSet()
    {
        // Given
        var validationCode = Guid.NewGuid().ToString();
        var domainEventGridWebhookHandler = A.Fake<IDomainEventGridWebhookHandler>();
        var subscriptionEventGridWebhookHandler = A.Fake<ISubscriptionEventGridWebhookHandler>();

        A.CallTo(() => subscriptionEventGridWebhookHandler.HandleEventGridSubscriptionEvent(A<SubscriptionValidationEventData>._))
            .Returns(new SubscriptionValidationResponse { ValidationResponse = validationCode });

        // When
        var eventGridRequestHandler = new EventGridRequestHandler(
            domainEventGridWebhookHandler,
            subscriptionEventGridWebhookHandler);

        var request = GetCloudEventSubscriptionRequest(validationCode);
        var result = await eventGridRequestHandler.HandleRequestAsync(request, this._subscriptionContext, CancellationToken.None);

        // Then
        Assert.NotNull(result);
        Assert.NotNull(result.ValidationResponse);
        Assert.Equal(validationCode, result.ValidationResponse.ValidationResponse);
        Assert.Equal(EventGridRequestType.Subscription, result.RequestType);
    }

    [Fact]
    public async Task GivenDomainEventEventGridRequest_WhenRequestContentValidAndContainsTelemetryCorrelationId_ThenRequestTelemetryParentIdIsSet()
    {
        // Given
        var domainEventGridWebhookHandler = A.Fake<IDomainEventGridWebhookHandler>();
        var subscriptionEventGridWebhookHandler = A.Fake<ISubscriptionEventGridWebhookHandler>();

        A.CallTo(() => domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(A<EventGridEvent>._, this._subscriptionContext, CancellationToken.None))
            .Returns(Task.CompletedTask);

        // When
        var eventGridRequestHandler = new EventGridRequestHandler(
            domainEventGridWebhookHandler,
            subscriptionEventGridWebhookHandler);

        var request = GetEventGridDomainEventRequest();
        var result = await eventGridRequestHandler.HandleRequestAsync(request, this._subscriptionContext, CancellationToken.None);

        // Then
        Assert.NotNull(result);
        Assert.Equal(EventGridRequestType.Event, result.RequestType);
    }

    [Fact]
    public async Task GivenDomainEventCloudEventRequest_WhenRequestContentValidAndContainsTelemetryCorrelationId_ThenRequestTelemetryParentIdIsSet()
    {
        // Given
        var domainEventGridWebhookHandler = A.Fake<IDomainEventGridWebhookHandler>();
        var subscriptionEventGridWebhookHandler = A.Fake<ISubscriptionEventGridWebhookHandler>();

        A.CallTo(() => domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(A<EventGridEvent>._, this._subscriptionContext, CancellationToken.None))
            .Returns(Task.CompletedTask);

        // When
        var eventGridRequestHandler = new EventGridRequestHandler(
            domainEventGridWebhookHandler,
            subscriptionEventGridWebhookHandler);

        var request = GetCloudEventDomainEventRequest();
        var result = await eventGridRequestHandler.HandleRequestAsync(request, this._subscriptionContext, CancellationToken.None);

        // Then
        Assert.NotNull(result);
        Assert.Equal(EventGridRequestType.Event, result.RequestType);
    }

    [Fact]
    public async Task GivenDomainEventEventGridRequest_WhenRequestThrowsExceptionAndContainsTelemetryCorrelationId_ThenRequestTelemetryIsTracked()
    {
        // Given
        var domainEventGridWebhookHandler = A.Fake<IDomainEventGridWebhookHandler>();
        var subscriptionEventGridWebhookHandler = A.Fake<ISubscriptionEventGridWebhookHandler>();
        A.CallTo(() => domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(A<EventGridEvent>._, this._subscriptionContext, CancellationToken.None))
            .Throws(new Exception("An exception was thrown"));

        // When
        var eventGridRequestHandler = new EventGridRequestHandler(
            domainEventGridWebhookHandler,
            subscriptionEventGridWebhookHandler);

        var request = GetEventGridDomainEventRequest();

        // Then
        await Assert.ThrowsAsync<Exception>(() => eventGridRequestHandler.HandleRequestAsync(request, this._subscriptionContext, CancellationToken.None));
    }

    [Fact]
    public async Task GivenDomainEventCloudEventRequest_WhenRequestThrowsExceptionAndContainsTelemetryCorrelationId_ThenRequestTelemetryIsTracked()
    {
        // Given
        var domainEventGridWebhookHandler = A.Fake<IDomainEventGridWebhookHandler>();
        var subscriptionEventGridWebhookHandler = A.Fake<ISubscriptionEventGridWebhookHandler>();
        A.CallTo(() => domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(A<CloudEvent>._, this._subscriptionContext, CancellationToken.None))
            .Throws(new Exception("An exception was thrown"));

        // When
        var eventGridRequestHandler = new EventGridRequestHandler(
            domainEventGridWebhookHandler,
            subscriptionEventGridWebhookHandler);

        var request = GetCloudEventDomainEventRequest();

        // Then
        await Assert.ThrowsAsync<Exception>(() => eventGridRequestHandler.HandleRequestAsync(request, this._subscriptionContext, CancellationToken.None));
    }

    [Fact]
    public async Task GivenDomainEventEventGridRequest_WhenRequestTelemetryNull_ThenOperationSucceeds()
    {
        // Given
        var domainEventGridWebhookHandler = A.Fake<IDomainEventGridWebhookHandler>();
        var subscriptionEventGridWebhookHandler = A.Fake<ISubscriptionEventGridWebhookHandler>();

        // When
        var eventGridRequestHandler = new EventGridRequestHandler(
            domainEventGridWebhookHandler,
            subscriptionEventGridWebhookHandler);

        var request = GetEventGridDomainEventRequest();

        await eventGridRequestHandler.HandleRequestAsync(request, this._subscriptionContext, CancellationToken.None);

        // Then
        A.CallTo(() => domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(A<EventGridEvent>._, this._subscriptionContext, CancellationToken.None))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GivenDomainEventCloudEventRequest_WhenRequestTelemetryNull_ThenOperationSucceeds()
    {
        // Given
        var domainEventGridWebhookHandler = A.Fake<IDomainEventGridWebhookHandler>();
        var subscriptionEventGridWebhookHandler = A.Fake<ISubscriptionEventGridWebhookHandler>();

        // When
        var eventGridRequestHandler = new EventGridRequestHandler(
            domainEventGridWebhookHandler,
            subscriptionEventGridWebhookHandler);

        var request = GetCloudEventDomainEventRequest();

        await eventGridRequestHandler.HandleRequestAsync(request, this._subscriptionContext, CancellationToken.None);

        // Then
        A.CallTo(() => domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(A<CloudEvent>._, this._subscriptionContext, CancellationToken.None))
            .MustHaveHappenedOnceExactly();
    }

    private static EventGridEvent[] GetEventGridSubscriptionRequest(string validationCode)
    {
        var eventGridEvent = new EventGridEvent(string.Empty, "Microsoft.EventGrid.SubscriptionValidationEvent", "1", new LocalSubscriptionValidationEventData(validationCode, "https://validationurl.io"))
        {
            Id = Guid.Parse("602a88ef-0001-00e6-1233-1646070610ea").ToString(),
            EventTime = DateTimeOffset.Parse("2017-08-16T01:57:26.005121Z"),
            Topic = "/subscriptions/topic-egt-",
        };

        return [eventGridEvent];
    }

    private static CloudEvent[] GetCloudEventSubscriptionRequest(string validationCode)
    {
        var cloudEvent = new CloudEvent("/subscriptions-topic-egt-", "Microsoft.EventGrid.SubscriptionValidationEvent", new LocalSubscriptionValidationEventData(validationCode, "https://validationurl.io"))
        {
            Id = Guid.Parse("602a88ef-0001-00e6-1233-1646070610ea").ToString(),
            Time = DateTimeOffset.Parse("2017-08-16T01:57:26.005121Z"),
        };

        return [cloudEvent];
    }

    private static EventGridEvent[] GetEventGridDomainEventRequest()
    {
        var eventGridEvent = new EventGridEvent("Test", "Workleap.Organization.DomainEvents.ExampleDomainEvent", "1", new ExampleDomainEvent(DateTimeOffset.Parse("2017-08-16T01:57:26.005121Z"), Guid.Parse("b59ff87c-9bfb-46b7-9092-04735202d2f6")))
        {
            Id = Guid.Parse("602a88ef-0001-00e6-1233-1646070610ea").ToString(),
            EventTime = DateTimeOffset.Parse("2017-08-16T01:57:26.005121Z"),
            Topic = "/subscriptions/topic-egt-",
        };

        return [eventGridEvent];
    }

    private static CloudEvent[] GetCloudEventDomainEventRequest()
    {
        var cloudEvent = new CloudEvent("/subscriptions/topic-egt-", "Workleap.Organization.DomainEvents.ExampleDomainEvent", new ExampleDomainEvent(DateTimeOffset.Parse("2017-08-16T01:57:26.005121Z"), Guid.Parse("b59ff87c-9bfb-46b7-9092-04735202d2f6")))
        {
            Id = Guid.Parse("602a88ef-0001-00e6-1233-1646070610ea").ToString(),
            Subject = "Test",
            Time = DateTimeOffset.Parse("2017-08-16T01:57:26.005121Z"),
        };

        return [cloudEvent];
    }
}

internal sealed record LocalSubscriptionValidationEventData(string ValidationCode, string ValidationUrl);

internal sealed record ExampleDomainEvent(DateTimeOffset EventDate, Guid EventId);