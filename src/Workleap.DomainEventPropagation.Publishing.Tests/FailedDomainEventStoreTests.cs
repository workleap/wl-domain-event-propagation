using System.Text.Json;
using System.Text.Json.Nodes;
using Azure;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.Namespaces;
using FakeItEasy;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Workleap.DomainEventPropagation.Publishing.Tests;

public class FailedDomainEventStoreTests
{
    private const string TopicEndpoint = "http://topicEndpoint.io";
    private const string EventGridEventName = "failed-store-eventgrid-event";
    private const string CloudEventName = "failed-store-cloud-event";

    private readonly EventGridPublisherClient _eventGridPublisherClient = A.Fake<EventGridPublisherClient>();
    private readonly IAzureClientFactory<EventGridPublisherClient> _eventGridPublisherClientFactory = A.Fake<IAzureClientFactory<EventGridPublisherClient>>();
    private readonly IAzureClientFactory<EventGridSenderClient> _eventGridSenderClientFactory = A.Fake<IAzureClientFactory<EventGridSenderClient>>();
    private readonly IFailedDomainEventStore _store = A.Fake<IFailedDomainEventStore>();
    private readonly EventPropagationClient _client;

    public FailedDomainEventStoreTests()
    {
        A.CallTo(() => this._eventGridPublisherClientFactory.CreateClient(EventPropagationPublisherOptions.EventGridClientName)).Returns(this._eventGridPublisherClient);
        A.CallTo(() => this._store.TryStoreAsync(A<IReadOnlyCollection<FailedDomainEvent>>._, A<EventPropagationPublishingException>._, A<CancellationToken>._)).Returns(true);

        this._client = this.CreateClient(this._store);
    }

    [Fact]
    public async Task GivenStoreAccepts_WhenPublishFails_ThenEventsAreStoredAndNoExceptionIsThrown()
    {
        // Given
        this.GivenEventGridSendFails();
        var domainEvents = new[]
        {
            new TestEventGridEvent { Text = "first", Number = 1 },
            new TestEventGridEvent { Text = "second", Number = 2 },
        };

        // When
        await this._client.PublishDomainEventsAsync(domainEvents, CancellationToken.None);

        // Then
        A.CallTo(() => this._store.TryStoreAsync(
                A<IReadOnlyCollection<FailedDomainEvent>>.That.Matches(events =>
                    events.Count == 2 &&
                    events.All(x => x.DomainEventName == EventGridEventName && x.Schema == EventSchema.EventGridEvent) &&
                    events.First().Data.Contains("\"text\":\"first\"") &&
                    events.Last().Data.Contains("\"text\":\"second\"")),
                A<EventPropagationPublishingException>.That.Matches(exception => exception.Message.Contains(EventGridEventName)),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GivenCallerTokenIsCancelled_WhenPublishFails_ThenStoreReceivesAnUncancelledToken()
    {
        // Given
        this.GivenEventGridSendFails();
        var cancelledToken = new CancellationToken(canceled: true);

        // When
        await this._client.PublishDomainEventAsync(new TestEventGridEvent { Text = "cancelled" }, cancelledToken);

        // Then
        A.CallTo(() => this._store.TryStoreAsync(
                A<IReadOnlyCollection<FailedDomainEvent>>._,
                A<EventPropagationPublishingException>._,
                A<CancellationToken>.That.Matches(token => !token.IsCancellationRequested)))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GivenStoreDeclines_WhenPublishFails_ThenThrowsPublishingException()
    {
        // Given
        this.GivenEventGridSendFails();
        A.CallTo(() => this._store.TryStoreAsync(A<IReadOnlyCollection<FailedDomainEvent>>._, A<EventPropagationPublishingException>._, A<CancellationToken>._)).Returns(false);

        // When
        var exception = await Assert.ThrowsAsync<EventPropagationPublishingException>(() => this._client.PublishDomainEventAsync(new TestEventGridEvent(), CancellationToken.None));

        // Then
        Assert.Contains(EventGridEventName, exception.Message);
    }

    [Fact]
    public async Task GivenStoreThrows_WhenPublishFails_ThenThrowsPublishingException()
    {
        // Given
        this.GivenEventGridSendFails();
        A.CallTo(() => this._store.TryStoreAsync(A<IReadOnlyCollection<FailedDomainEvent>>._, A<EventPropagationPublishingException>._, A<CancellationToken>._)).Throws(new InvalidOperationException("Storage unavailable"));

        // When
        var exception = await Assert.ThrowsAsync<EventPropagationPublishingException>(() => this._client.PublishDomainEventAsync(new TestEventGridEvent(), CancellationToken.None));

        // Then
        Assert.Contains(EventGridEventName, exception.Message);
    }

    [Fact]
    public async Task GivenPublishSucceeds_WhenPublish_ThenStoreIsNotCalled()
    {
        // When
        await this._client.PublishDomainEventAsync(new TestEventGridEvent(), CancellationToken.None);

        // Then
        A.CallTo(() => this._store.TryStoreAsync(A<IReadOnlyCollection<FailedDomainEvent>>._, A<EventPropagationPublishingException>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task GivenMetadataConfiguration_WhenPublishFails_ThenEventsAreNotStoredAndExceptionIsThrown()
    {
        // Given
        A.CallTo(() => this._eventGridPublisherClient.SendEventsAsync(A<IEnumerable<CloudEvent>>._, A<CancellationToken>._)).Throws(new InvalidOperationException("Event Grid unavailable"));

        // When
        await Assert.ThrowsAsync<EventPropagationPublishingException>(() => this._client.PublishDomainEventAsync(new TestCloudEvent(), x => x.Subject = "subject", CancellationToken.None));

        // Then
        A.CallTo(() => this._store.TryStoreAsync(A<IReadOnlyCollection<FailedDomainEvent>>._, A<EventPropagationPublishingException>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task GivenNoStore_WhenPublishFails_ThenThrowsPublishingException()
    {
        // Given
        this.GivenEventGridSendFails();
        var client = this.CreateClient(store: null);

        // When
        var exception = await Assert.ThrowsAsync<EventPropagationPublishingException>(() => client.PublishDomainEventAsync(new TestEventGridEvent(), CancellationToken.None));

        // Then
        Assert.Contains(EventGridEventName, exception.Message);
    }

    [Fact]
    public async Task GivenStoredEventGridEvent_WhenRepublish_ThenSendsTheSamePayload()
    {
        // Given
        var storedEvent = await this.CaptureStoredEventAsync(new TestEventGridEvent { Text = "replayed", Number = 42 });
        A.CallTo(() => this._eventGridPublisherClient.SendEventsAsync(A<IEnumerable<EventGridEvent>>._, A<CancellationToken>._)).Returns(A.Fake<Response>());

        // When
        await this._client.RepublishAsync(storedEvent, CancellationToken.None);

        // Then
        A.CallTo(() => this._eventGridPublisherClient.SendEventsAsync(
                A<IEnumerable<EventGridEvent>>.That.Matches(events => IsReplayedEventGridEvent(events)),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GivenStoredCloudEvent_WhenRepublish_ThenSendsCloudEvent()
    {
        // Given
        var storedEvent = new FailedDomainEvent(CloudEventName, EventSchema.CloudEvent, "{\"text\":\"cloud\",\"number\":7}");

        // When
        await this._client.RepublishAsync(storedEvent, CancellationToken.None);

        // Then
        A.CallTo(() => this._eventGridPublisherClient.SendEventsAsync(
                A<IEnumerable<CloudEvent>>.That.Matches(events => events.Single().Type == CloudEventName && events.Single().Data!.ToObjectFromJson<JsonObject>()!["text"]!.GetValue<string>() == "cloud"),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GivenPublishFails_WhenRepublish_ThenThrowsAndDoesNotStoreAgain()
    {
        // Given
        this.GivenEventGridSendFails();
        var storedEvent = new FailedDomainEvent(EventGridEventName, EventSchema.EventGridEvent, "{\"text\":\"still failing\"}");

        // When
        var exception = await Assert.ThrowsAsync<EventPropagationPublishingException>(() => this._client.RepublishAsync(storedEvent, CancellationToken.None));

        // Then
        Assert.Contains(EventGridEventName, exception.Message);
        A.CallTo(() => this._store.TryStoreAsync(A<IReadOnlyCollection<FailedDomainEvent>>._, A<EventPropagationPublishingException>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task GivenDataIsNotAJsonObject_WhenRepublish_ThenThrowsArgumentException()
    {
        // Given
        var storedEvent = new FailedDomainEvent(EventGridEventName, EventSchema.EventGridEvent, "[1,2,3]");

        // When
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => this._client.RepublishAsync(storedEvent, CancellationToken.None));

        // Then
        Assert.Equal("data", exception.ParamName);
    }

    [Fact]
    public async Task GivenDataIsNotValidJson_WhenRepublish_ThenThrowsArgumentExceptionWithoutSending()
    {
        // Given
        var storedEvent = new FailedDomainEvent(EventGridEventName, EventSchema.EventGridEvent, "{not json");

        // When
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => this._client.RepublishAsync(storedEvent, CancellationToken.None));

        // Then
        Assert.Equal("data", exception.ParamName);
        Assert.IsAssignableFrom<JsonException>(exception.InnerException);
        A.CallTo(() => this._eventGridPublisherClient.SendEventsAsync(A<IEnumerable<EventGridEvent>>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task GivenBehaviorInjectsTraceContext_WhenPublishFails_ThenStoredDataContainsIt()
    {
        // Given
        const string traceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
        var client = this.CreateClient(this._store, new TraceContextInjectingBehavior(traceParent));
        this.GivenEventGridSendFails();

        FailedDomainEvent? storedEvent = null;
        A.CallTo(() => this._store.TryStoreAsync(A<IReadOnlyCollection<FailedDomainEvent>>._, A<EventPropagationPublishingException>._, A<CancellationToken>._))
            .Invokes((IReadOnlyCollection<FailedDomainEvent> events, EventPropagationPublishingException _, CancellationToken _) => storedEvent = events.Single())
            .Returns(true);

        // When
        await client.PublishDomainEventAsync(new TestEventGridEvent { Text = "traced" }, CancellationToken.None);

        // Then
        Assert.NotNull(storedEvent);
        var storedData = JsonNode.Parse(storedEvent.Data)!;
        Assert.Equal(traceParent, storedData["__traceparent"]?.GetValue<string>());
        Assert.Equal("traced", storedData["text"]?.GetValue<string>());
    }

    [Fact]
    public async Task GivenNullEvent_WhenRepublish_ThenThrowsArgumentNullException()
    {
        // When
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => this._client.RepublishAsync(null!, CancellationToken.None));

        // Then
        Assert.Equal("domainEvent", exception.ParamName);
    }

    [Fact]
    public void GivenNullValues_WhenCreateFailedDomainEvent_ThenThrowsArgumentNullException()
    {
        Assert.Equal("domainEventName", Assert.Throws<ArgumentNullException>(() => new FailedDomainEvent(null!, EventSchema.CloudEvent, "{}")).ParamName);
        Assert.Equal("data", Assert.Throws<ArgumentNullException>(() => new FailedDomainEvent("name", EventSchema.CloudEvent, null!)).ParamName);
    }

    [Fact]
    public void GivenStoreRegistered_WhenResolveServices_ThenStoreAndRepublisherAreWired()
    {
        // Given
        var services = CreateServiceCollection();

        // When
        services.AddEventPropagationPublisher().AddFailedDomainEventStore<InMemoryFailedDomainEventStore>();
        using var serviceProvider = services.BuildServiceProvider();

        // Then
        Assert.IsType<InMemoryFailedDomainEventStore>(serviceProvider.GetRequiredService<IFailedDomainEventStore>());
        Assert.IsType<EventPropagationClient>(serviceProvider.GetRequiredService<IFailedDomainEventRepublisher>());
    }

    [Fact]
    public void GivenNoStoreRegistered_WhenResolveServices_ThenRepublisherIsStillAvailable()
    {
        // Given
        var services = CreateServiceCollection();

        // When
        services.AddEventPropagationPublisher();
        using var serviceProvider = services.BuildServiceProvider();

        // Then
        Assert.Null(serviceProvider.GetService<IFailedDomainEventStore>());
        Assert.IsType<EventPropagationClient>(serviceProvider.GetRequiredService<IFailedDomainEventRepublisher>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GivenPublisherRegistered_WhenInspectClientRegistration_ThenItIsUnchangedFromPreviousVersions(bool withStore)
    {
        // Given
        var services = CreateServiceCollection();

        // When
        var builder = services.AddEventPropagationPublisher();
        if (withStore)
        {
            builder.AddFailedDomainEventStore<InMemoryFailedDomainEventStore>();
        }

        // Then
        var clientDescriptor = Assert.Single(services, x => x.ServiceType == typeof(IEventPropagationClient));
        Assert.Equal(typeof(EventPropagationClient), clientDescriptor.ImplementationType);
        Assert.Null(clientDescriptor.ImplementationFactory);
        Assert.Equal(ServiceLifetime.Singleton, clientDescriptor.Lifetime);
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(EventPropagationClient));
    }

    [Fact]
    public void GivenClientRegisteredByConsumer_WhenAddEventPropagationPublisher_ThenConsumerClientIsKept()
    {
        // Given
        var services = CreateServiceCollection();
        var consumerClient = A.Fake<IEventPropagationClient>();
        services.AddSingleton(consumerClient);

        // When
        services.AddEventPropagationPublisher();
        using var serviceProvider = services.BuildServiceProvider();

        // Then
        Assert.Same(consumerClient, serviceProvider.GetRequiredService<IEventPropagationClient>());
    }

    [Fact]
    public void GivenNullBuilder_WhenAddFailedDomainEventStore_ThenThrowsArgumentNullException()
    {
        // When
        var exception = Assert.Throws<ArgumentNullException>(() => ((IEventPropagationPublisherBuilder)null!).AddFailedDomainEventStore<InMemoryFailedDomainEventStore>());

        // Then
        Assert.Equal("builder", exception.ParamName);
    }

    private static ServiceCollection CreateServiceCollection()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{EventPropagationPublisherOptions.SectionName}:TopicEndpoint"] = TopicEndpoint,
            [$"{EventPropagationPublisherOptions.SectionName}:TopicAccessKey"] = "topicAccessKey",
        }).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        return services;
    }

    private static bool IsReplayedEventGridEvent(IEnumerable<EventGridEvent> events)
    {
        var replayedEvent = events.Single();
        var data = replayedEvent.Data.ToObjectFromJson<JsonObject>()!;

        return replayedEvent.EventType == EventGridEventName &&
               replayedEvent.Subject == EventGridEventName &&
               data["text"]!.GetValue<string>() == "replayed" &&
               data["number"]!.GetValue<int>() == 42;
    }

    private EventPropagationClient CreateClient(IFailedDomainEventStore? store, params IPublishingDomainEventBehavior[] behaviors)
    {
        var options = Options.Create(new EventPropagationPublisherOptions
        {
            TopicType = TopicType.Custom,
            TopicEndpoint = TopicEndpoint,
            TopicAccessKey = "topicAccessKey",
        });

        return new EventPropagationClient(
            this._eventGridPublisherClientFactory,
            this._eventGridSenderClientFactory,
            options,
            behaviors,
            store == null ? Array.Empty<IFailedDomainEventStore>() : new[] { store });
    }

    private void GivenEventGridSendFails()
    {
        A.CallTo(() => this._eventGridPublisherClient.SendEventsAsync(A<IEnumerable<EventGridEvent>>._, A<CancellationToken>._)).Throws(new InvalidOperationException("Event Grid unavailable"));
    }

    private async Task<FailedDomainEvent> CaptureStoredEventAsync(TestEventGridEvent domainEvent)
    {
        FailedDomainEvent? storedEvent = null;
        this.GivenEventGridSendFails();
        A.CallTo(() => this._store.TryStoreAsync(A<IReadOnlyCollection<FailedDomainEvent>>._, A<EventPropagationPublishingException>._, A<CancellationToken>._))
            .Invokes((IReadOnlyCollection<FailedDomainEvent> events, EventPropagationPublishingException _, CancellationToken _) => storedEvent = events.Single())
            .Returns(true);

        await this._client.PublishDomainEventAsync(domainEvent, CancellationToken.None);

        Fake.ClearRecordedCalls(this._eventGridPublisherClient);

        return storedEvent ?? throw new InvalidOperationException("The event was not stored");
    }

    [DomainEvent(EventGridEventName, EventSchema.EventGridEvent)]
    private sealed class TestEventGridEvent : IDomainEvent
    {
        public string Text { get; set; } = string.Empty;

        public int Number { get; set; }
    }

    [DomainEvent(CloudEventName, EventSchema.CloudEvent)]
    private sealed class TestCloudEvent : IDomainEvent
    {
        public string Text { get; set; } = string.Empty;
    }

    // Writes metadata the same way TracingPublishingDomainEventBehavior does, without depending on global OpenTelemetry state
    private sealed class TraceContextInjectingBehavior(string traceParent) : IPublishingDomainEventBehavior
    {
        public Task HandleAsync(DomainEventWrapperCollection domainEventWrappers, DomainEventsHandlerDelegate next, CancellationToken cancellationToken)
        {
            foreach (var domainEventWrapper in domainEventWrappers)
            {
                domainEventWrapper.SetMetadata("traceparent", traceParent);
            }

            return next(domainEventWrappers, cancellationToken);
        }
    }

    private sealed class InMemoryFailedDomainEventStore : IFailedDomainEventStore
    {
        public Task<bool> TryStoreAsync(IReadOnlyCollection<FailedDomainEvent> domainEvents, EventPropagationPublishingException exception, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }
    }
}