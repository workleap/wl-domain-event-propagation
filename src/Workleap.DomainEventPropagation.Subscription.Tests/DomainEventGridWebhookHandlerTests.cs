using Azure.Messaging;
using Azure.Messaging.EventGrid;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Workleap.DomainEventPropagation.Subscription.Tests;

public class DomainEventGridWebhookHandlerTests
{
    private static readonly DomainEventWrapper DomainEvent = DomainEventWrapper.Wrap(new TestDomainEvent() { Number = 1, Text = "Hello world" });

    private readonly IServiceProvider _serviceProvider = A.Fake<IServiceProvider>();
    private readonly IDomainEventTypeRegistry _domainEventRegistry = A.Fake<IDomainEventTypeRegistry>();
    private readonly IDomainEventHandler<TestDomainEvent> _domainEventHandler = A.Fake<IDomainEventHandler<TestDomainEvent>>();
    private readonly ILogger<DomainEventGridWebhookHandler> _logger = A.Fake<ILogger<DomainEventGridWebhookHandler>>();

    private readonly EventGridEvent _eventGridEvent = new EventGridEvent("subject", DomainEvent.DomainEventName, "1.0", BinaryData.FromObjectAsJson(DomainEvent));
    private readonly CloudEvent _cloudEvent = new CloudEvent("source", DomainEvent.DomainEventName, DomainEvent);
    private readonly IDomainEventSubscriptionContext _subscriptionContext = A.Fake<IDomainEventSubscriptionContext>();

    [Fact]
    public async Task GivenEventGridDomainEventIsFired_WhenDomainEventTypeUnknown_ThenDomainEventIsIgnored()
    {
        // Given
        A.CallTo(() => this._domainEventRegistry.GetDomainEventType(DomainEvent.DomainEventName)).Returns(null);

        // When
        var domainEventGridWebhookHandler = new DomainEventGridWebhookHandler(
            this._serviceProvider,
            this._domainEventRegistry,
            this._logger,
            Array.Empty<ISubscriptionDomainEventBehavior>());

        await domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(this._eventGridEvent, this._subscriptionContext, CancellationToken.None);

        // Then
        A.CallTo(() => this._domainEventHandler.HandleDomainEventAsync(A<TestDomainEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task GivenCloudDomainEventIsFired_WhenDomainEventTypeUnknown_ThenDomainEventIsIgnored()
    {
        // Given
        A.CallTo(() => this._domainEventRegistry.GetDomainEventType(DomainEvent.DomainEventName)).Returns(null);

        // When
        var domainEventGridWebhookHandler = new DomainEventGridWebhookHandler(
            this._serviceProvider,
            this._domainEventRegistry,
            this._logger,
            Array.Empty<ISubscriptionDomainEventBehavior>());

        await domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(this._cloudEvent, this._subscriptionContext, CancellationToken.None);

        // Then
        A.CallTo(() => this._domainEventHandler.HandleDomainEventAsync(A<TestDomainEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task GivenEventGridDomainEventIsFired_WhenDomainEventHandlerNull_ThenDomainEventIsIgnored()
    {
        // Given
        A.CallTo(() => this._serviceProvider.GetService(A<Type>._)).Returns(null);

        // When
        var domainEventGridWebhookHandler = new DomainEventGridWebhookHandler(
            this._serviceProvider,
            this._domainEventRegistry,
            this._logger,
            Array.Empty<ISubscriptionDomainEventBehavior>());

        await domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(this._eventGridEvent, this._subscriptionContext, CancellationToken.None);

        // Then
        A.CallTo(() => this._domainEventHandler.HandleDomainEventAsync(A<TestDomainEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task GivenCloudDomainEventIsFired_WhenDomainEventHandlerNull_ThenDomainEventIsIgnored()
    {
        // Given
        A.CallTo(() => this._serviceProvider.GetService(A<Type>._)).Returns(null);

        // When
        var domainEventGridWebhookHandler = new DomainEventGridWebhookHandler(
            this._serviceProvider,
            this._domainEventRegistry,
            this._logger,
            Array.Empty<ISubscriptionDomainEventBehavior>());

        await domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(this._cloudEvent, this._subscriptionContext, CancellationToken.None);

        // Then
        A.CallTo(() => this._domainEventHandler.HandleDomainEventAsync(A<TestDomainEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task GivenEventGridDomainEventIsFired_WhenHandleMethodDoesntExistOnHandler_ThenThrowsException()
    {
        // Given
        A.CallTo(() => this._domainEventRegistry.GetDomainEventType(A<string>._)).Returns(typeof(TestDomainEvent));
        A.CallTo(() => this._domainEventRegistry.GetDomainEventHandlerType(A<string>._)).Returns(typeof(FakeDomainEventHandler));

        // When
        var domainEventGridWebhookHandler = new DomainEventGridWebhookHandler(
            this._serviceProvider,
            this._domainEventRegistry,
            this._logger,
            Array.Empty<ISubscriptionDomainEventBehavior>());

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(this._eventGridEvent, this._subscriptionContext, CancellationToken.None));

        // Then
        A.CallTo(() => this._domainEventHandler.HandleDomainEventAsync(A<TestDomainEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task GivenCloudDomainEventIsFired_WhenHandleMethodDoesntExistOnHandler_ThenThrowsException()
    {
        // Given
        A.CallTo(() => this._domainEventRegistry.GetDomainEventType(A<string>._)).Returns(typeof(TestDomainEvent));
        A.CallTo(() => this._domainEventRegistry.GetDomainEventHandlerType(A<string>._)).Returns(typeof(FakeDomainEventHandler));

        // When
        var domainEventGridWebhookHandler = new DomainEventGridWebhookHandler(
            this._serviceProvider,
            this._domainEventRegistry,
            this._logger,
            Array.Empty<ISubscriptionDomainEventBehavior>());

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(this._cloudEvent, this._subscriptionContext, CancellationToken.None));

        // Then
        A.CallTo(() => this._domainEventHandler.HandleDomainEventAsync(A<TestDomainEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task GivenEventGridDomainEventIsFired_WhenDomainEventHandlerExists_ThenEventHandled()
    {
        // Given
        A.CallTo(() => this._serviceProvider.GetService(A<Type>._)).Returns(this._domainEventHandler);
        A.CallTo(() => this._domainEventRegistry.GetDomainEventType(A<string>._)).Returns(typeof(TestDomainEvent));
        A.CallTo(() => this._domainEventRegistry.GetDomainEventHandlerType(A<string>._)).Returns(typeof(IDomainEventHandler<TestDomainEvent>));

        // When
        var domainEventGridWebhookHandler = new DomainEventGridWebhookHandler(
            this._serviceProvider,
            this._domainEventRegistry,
            this._logger,
            Array.Empty<ISubscriptionDomainEventBehavior>());

        await domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(this._eventGridEvent, this._subscriptionContext, CancellationToken.None);

        // Then
        A.CallTo(() => this._domainEventHandler.HandleDomainEventAsync(A<TestDomainEvent>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GivenCloudDomainEventIsFired_WhenDomainEventHandlerExists_ThenEventHandled()
    {
        // Given
        A.CallTo(() => this._serviceProvider.GetService(A<Type>._)).Returns(this._domainEventHandler);
        A.CallTo(() => this._domainEventRegistry.GetDomainEventType(A<string>._)).Returns(typeof(TestDomainEvent));
        A.CallTo(() => this._domainEventRegistry.GetDomainEventHandlerType(A<string>._)).Returns(typeof(IDomainEventHandler<TestDomainEvent>));

        // When
        var domainEventGridWebhookHandler = new DomainEventGridWebhookHandler(
            this._serviceProvider,
            this._domainEventRegistry,
            this._logger,
            Array.Empty<ISubscriptionDomainEventBehavior>());

        await domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(this._cloudEvent, this._subscriptionContext, CancellationToken.None);

        // Then
        A.CallTo(() => this._domainEventHandler.HandleDomainEventAsync(A<TestDomainEvent>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void GivenRegisteredDomainEventHandlers_WhenResolvingDomainEventHandlers_ThenDomainEventHandlersAreResolved()
    {
        // Given
        var expectedDomainEventHandlerTypes = typeof(DomainEventGridWebhookHandlerTests)
            .Assembly.GetTypes()
            .Where(p => !p.IsInterface && !p.IsAbstract && p.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>)))
            .ToList();

        var services = new ServiceCollection();
        var eventProcessingBuilder = services.AddEventPropagationSubscriber();

        // When
        eventProcessingBuilder.AddDomainEventHandlers(typeof(DomainEventGridWebhookHandlerTests).Assembly);

        // Then
        var unregisteredDomainEventHandlerTypes = new List<Type>();
        var provider = services.BuildServiceProvider();

        foreach (var domainEventHandlerType in expectedDomainEventHandlerTypes)
        {
            try
            {
                provider.GetService(domainEventHandlerType);
            }
            catch (Exception)
            {
                unregisteredDomainEventHandlerTypes.Add(domainEventHandlerType);
            }
        }

        if (unregisteredDomainEventHandlerTypes.Count > 0)
        {
            Assert.Fail($"Some domain event handlers, or their dependencies, were not registered: {string.Join(", ", unregisteredDomainEventHandlerTypes.Select(x => x.FullName))}");
        }
    }

    [Fact]
    public async Task GivenRegisteredTracingBehavior_WhenHandleEventGridWebhookEventAsyncForEventGridEvent_ThenBehaviorCalled()
    {
        // Given
        var subscriberBehavior = A.Fake<ISubscriptionDomainEventBehavior>();
        var eventHandler = A.Fake<IDomainEventHandler<TestDomainEvent>>();

        var services = new ServiceCollection();
        services.AddSingleton(subscriberBehavior);
        services.AddSingleton(eventHandler);
        var serviceProvider = services.BuildServiceProvider();

        // When
        var webhookHandler = new DomainEventGridWebhookHandler(serviceProvider, A.Fake<IDomainEventTypeRegistry>(), NullLogger<DomainEventGridWebhookHandler>.Instance, new[] { subscriberBehavior });

        await webhookHandler.HandleEventGridWebhookEventAsync(this._eventGridEvent, this._subscriptionContext, CancellationToken.None);

        // Then
        A.CallTo(() => subscriberBehavior.HandleAsync(A<DomainEventWrapper>._, this._subscriptionContext, A<SubscriptionDomainEventHandlerDelegate>._, A<CancellationToken>._)).MustHaveHappened();
    }

    [Fact]
    public async Task GivenRegisteredTracingBehavior_WhenHandleEventGridWebhookEventAsyncForEventCloudEvent_ThenBehaviorCalled()
    {
        // Given
        var subscriberBehavior = A.Fake<ISubscriptionDomainEventBehavior>();
        var eventHandler = A.Fake<IDomainEventHandler<TestDomainEvent>>();

        var services = new ServiceCollection();
        services.AddSingleton(subscriberBehavior);
        services.AddSingleton(eventHandler);
        var serviceProvider = services.BuildServiceProvider();

        // When
        var webhookHandler = new DomainEventGridWebhookHandler(serviceProvider, A.Fake<IDomainEventTypeRegistry>(), NullLogger<DomainEventGridWebhookHandler>.Instance, new[] { subscriberBehavior });

        await webhookHandler.HandleEventGridWebhookEventAsync(this._cloudEvent, this._subscriptionContext, CancellationToken.None);

        // Then
        A.CallTo(() => subscriberBehavior.HandleAsync(A<DomainEventWrapper>._, this._subscriptionContext, A<SubscriptionDomainEventHandlerDelegate>._, A<CancellationToken>._)).MustHaveHappened();
    }

    [DomainEvent("test")]
    public class TestDomainEvent : IDomainEvent
    {
        public string Text { get; set; } = string.Empty;

        public int Number { get; set; }
    }

    private sealed class FakeDomainEventHandler
    {
    }
}