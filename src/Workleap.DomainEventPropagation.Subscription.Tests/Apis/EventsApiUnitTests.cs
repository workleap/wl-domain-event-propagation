using System.Net;
using System.Text.Json;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using FakeItEasy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Workleap.DomainEventPropagation.Subscription.Tests.Apis;

public class EventsApiUnitTests
{
    private readonly IEventGridRequestHandler _eventGridRequestHandler;

    public EventsApiUnitTests()
    {
        this._eventGridRequestHandler = A.Fake<IEventGridRequestHandler>(x => x.Strict());
    }

    [Fact]
    public async Task GivenEventsApiHandleEventGridEvent_WhenResultRequestTypeIsSubscription_ThenReturnsOkResultWithValidationCode()
    {
        // Given
        var subscriptionValidationResponse = new SubscriptionValidationResponse
        {
            ValidationResponse = "abcdef",
        };

        var httpRequest = await CreateHttpRequest(GenerateEventGridEvents());
        var eventGridRequestResult = new EventGridRequestResult(EventGridRequestType.Subscription, subscriptionValidationResponse);

        A.CallTo(() => this._eventGridRequestHandler.HandleRequestAsync(A<EventGridEvent[]>._, A<DomainEventSubscriptionContext>._, A<CancellationToken>._)).Returns(Task.FromResult(eventGridRequestResult));

        // When
        var actualResult = await EventsApi.HandleEvents(httpRequest, this._eventGridRequestHandler, CancellationToken.None);

        // Then
        var (data, statusCode) = await actualResult.GetResponseAsync<SubscriptionValidationResponse>();

        Assert.Equal(HttpStatusCode.OK, statusCode);
        Assert.NotNull(data);
        Assert.Equal(subscriptionValidationResponse.ValidationResponse, data.ValidationResponse);
    }

    [Fact]
    public async Task GivenEventsApiHandleCloudEvent_WhenResultRequestTypeIsSubscription_ThenReturnsOkResultWithValidationCode()
    {
        // Given
        var subscriptionValidationResponse = new SubscriptionValidationResponse
        {
            ValidationResponse = "abcdef",
        };

        var httpRequest = await CreateHttpRequest(GenerateCloudEvents());
        var eventGridRequestResult = new EventGridRequestResult(EventGridRequestType.Subscription, subscriptionValidationResponse);

        A.CallTo(() => this._eventGridRequestHandler.HandleRequestAsync(A<CloudEvent[]>._, A<DomainEventSubscriptionContext>._, A<CancellationToken>._)).Returns(Task.FromResult(eventGridRequestResult));

        // When
        var actualResult = await EventsApi.HandleEvents(httpRequest, this._eventGridRequestHandler, CancellationToken.None);

        // Then
        var (data, statusCode) = await actualResult.GetResponseAsync<SubscriptionValidationResponse>();

        Assert.Equal(HttpStatusCode.OK, statusCode);
        Assert.NotNull(data);
        Assert.Equal(subscriptionValidationResponse.ValidationResponse, data.ValidationResponse);
    }

    [Fact]
    public async Task GivenEventsApiHandleEventGridEvent_WhenResultRequestTypeIsNotSubscription_ThenReturnsOkResult()
    {
        // Given
        var eventGridRequestResult = new EventGridRequestResult(EventGridRequestType.Event);

        var httpRequest = await CreateHttpRequest(GenerateEventGridEvents());
        A.CallTo(() => this._eventGridRequestHandler.HandleRequestAsync(A<EventGridEvent[]>._, A<DomainEventSubscriptionContext>._, A<CancellationToken>._)).Returns(Task.FromResult(eventGridRequestResult));

        // When
        var actualResult = await EventsApi.HandleEvents(httpRequest, this._eventGridRequestHandler, CancellationToken.None);

        // Then
        var (data, statusCode) = await actualResult.GetResponseAsync<object>();

        Assert.Equal(HttpStatusCode.OK, statusCode);
        Assert.Null(data);
    }

    [Fact]
    public async Task GivenEventsApiHandleCloudEvent_WhenResultRequestTypeIsNotSubscription_ThenReturnsOkResult()
    {
        // Given
        var eventGridRequestResult = new EventGridRequestResult(EventGridRequestType.Event);

        var httpRequest = await CreateHttpRequest(GenerateCloudEvents());
        A.CallTo(() => this._eventGridRequestHandler.HandleRequestAsync(A<CloudEvent[]>._, A<DomainEventSubscriptionContext>._, A<CancellationToken>._)).Returns(Task.FromResult(eventGridRequestResult));

        // When
        var actualResult = await EventsApi.HandleEvents(httpRequest, this._eventGridRequestHandler, CancellationToken.None);

        // Then
        var (data, statusCode) = await actualResult.GetResponseAsync<object>();

        Assert.Equal(HttpStatusCode.OK, statusCode);
        Assert.Null(data);
    }

    [Fact]
    public async Task GivenEventsApiHandleEvent_WhenEventIsNeitherEventGridEventOrCloudEvent_ThenThrows()
    {
        // Given
        var httpRequest = await CreateHttpRequest(new { Unknown = "Event" });

        // When
        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => EventsApi.HandleEvents(httpRequest, this._eventGridRequestHandler, CancellationToken.None));

        // Then
        Assert.NotNull(ex);
        Assert.Contains("Unknown payload", ex.Message);
    }

    private static EventGridEvent[] GenerateEventGridEvents()
    {
        var eventGridEvent = new EventGridEvent("subject", "Workleap.DomainEventPropagation.Dummy.Type", "1.0", new BinaryData(new { id = Guid.NewGuid() }))
        {
            Topic = "topic",
        };

        return [eventGridEvent];
    }

    private static CloudEvent[] GenerateCloudEvents()
    {
        var cloudEvent = new CloudEvent("source", "Workleap.DomainEventPropagation.Dummy.Type", new { id = Guid.NewGuid() })
        {
            Subject = "subject",
        };

        return [cloudEvent];
    }

    private static async Task<HttpRequest> CreateHttpRequest(object body)
    {
        var httpContext = new DefaultHttpContext
        {
            Request =
            {
                Method = "POST",
                Scheme = "http",
                Host = new HostString("localhost"),
                ContentType = "application/json",
            },
        };
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        await writer.WriteAsync(JsonSerializer.Serialize(body));
        await writer.FlushAsync();
        stream.Position = 0;
        httpContext.Request.Body = stream;

        return httpContext.Request;
    }
}