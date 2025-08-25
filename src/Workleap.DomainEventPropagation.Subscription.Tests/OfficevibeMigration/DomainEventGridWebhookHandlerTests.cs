using Azure.Messaging.EventGrid;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Officevibe.DomainEvents;

namespace Workleap.DomainEventPropagation.Subscription.Tests.OfficevibeMigration;

public class DomainEventGridWebhookHandlerTests
{
    private static readonly OfficevibeEvent DomainEvent = new() { Number = 1, Text = "Hello world" };

    private readonly IServiceProvider _serviceProvider = A.Fake<IServiceProvider>();
    private readonly IDomainEventTypeRegistry _domainEventRegistry = A.Fake<IDomainEventTypeRegistry>();
    private readonly IDomainEventHandler<OfficevibeEvent> _domainEventHandler = A.Fake<IDomainEventHandler<OfficevibeEvent>>();
    private readonly ILogger<DomainEventGridWebhookHandler> _logger = A.Fake<ILogger<DomainEventGridWebhookHandler>>();

    private readonly EventGridEvent _eventGridEvent = new EventGridEvent("subject", DomainEvent.GetType().FullName, "1.0", BinaryData.FromObjectAsJson(DomainEvent));

    [Fact]
    public async Task GivenDomainEventIsFired_WhenDomainEventTypeUnknown_ThenDomainEventIsIgnored()
    {
        // Given
        A.CallTo(() => this._domainEventRegistry.GetDomainEventType(A<string>._)).Returns(null);

        // When
        var domainEventGridWebhookHandler = new DomainEventGridWebhookHandler(
            this._serviceProvider,
            this._domainEventRegistry,
            this._logger,
            Array.Empty<ISubscriptionDomainEventBehavior>());

        await domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(this._eventGridEvent, CancellationToken.None);

        // Then
        A.CallTo(() => this._domainEventHandler.HandleDomainEventAsync(A<OfficevibeEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task GivenDomainEventIsFired_WhenDomainEventHandlerNull_ThenDomainEventIsIgnored()
    {
        // Given
        A.CallTo(() => this._serviceProvider.GetService(A<Type>._)).Returns(null);

        // When
        var domainEventGridWebhookHandler = new DomainEventGridWebhookHandler(
            this._serviceProvider,
            this._domainEventRegistry,
            this._logger,
            Array.Empty<ISubscriptionDomainEventBehavior>());

        await domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(this._eventGridEvent, CancellationToken.None);

        // Then
        A.CallTo(() => this._domainEventHandler.HandleDomainEventAsync(A<OfficevibeEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task GivenDomainEventIsFired_WhenHandleMethodDoesntExistOnHandler_ThenThrowsException()
    {
        // Given
        A.CallTo(() => this._domainEventRegistry.GetDomainEventType(A<string>._)).Returns(typeof(OfficevibeEvent));
        A.CallTo(() => this._domainEventRegistry.GetDomainEventHandlerType(A<string>._)).Returns(typeof(FakeDomainEventHandler));

        // When
        var domainEventGridWebhookHandler = new DomainEventGridWebhookHandler(
            this._serviceProvider,
            this._domainEventRegistry,
            this._logger,
            Array.Empty<ISubscriptionDomainEventBehavior>());

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(this._eventGridEvent, CancellationToken.None));

        // Then
        A.CallTo(() => this._domainEventHandler.HandleDomainEventAsync(A<OfficevibeEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task GivenDomainEventIsFired_WhenDomainEventHandlerExists_ThenEventHandled()
    {
        // Given
        A.CallTo(() => this._serviceProvider.GetService(A<Type>._)).Returns(this._domainEventHandler);
        A.CallTo(() => this._domainEventRegistry.GetDomainEventType(A<string>._)).Returns(typeof(OfficevibeEvent));
        A.CallTo(() => this._domainEventRegistry.GetDomainEventHandlerType(A<string>._)).Returns(typeof(IDomainEventHandler<OfficevibeEvent>));

        // When
        var domainEventGridWebhookHandler = new DomainEventGridWebhookHandler(
            this._serviceProvider,
            this._domainEventRegistry,
            this._logger,
            Array.Empty<ISubscriptionDomainEventBehavior>());

        await domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(this._eventGridEvent, CancellationToken.None);

        // Then
        A.CallTo(() => this._domainEventHandler.HandleDomainEventAsync(A<OfficevibeEvent>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GivenOfficevibeDomainEventIsFired_WhenDomainEventHandlerExists_ThenEventHandled()
    {
        // Given
        A.CallTo(() => this._serviceProvider.GetService(A<Type>._)).Returns(this._domainEventHandler);
        A.CallTo(() => this._domainEventRegistry.GetDomainEventType(A<string>._)).Returns(typeof(OfficevibeEvent));
        A.CallTo(() => this._domainEventRegistry.GetDomainEventHandlerType(A<string>._)).Returns(typeof(IDomainEventHandler<OfficevibeEvent>));

        // When
        var domainEventGridWebhookHandler = new DomainEventGridWebhookHandler(
            this._serviceProvider,
            this._domainEventRegistry,
            this._logger,
            Array.Empty<ISubscriptionDomainEventBehavior>());

        await domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(this._eventGridEvent, CancellationToken.None);

        // Then
        A.CallTo(() => this._domainEventHandler.HandleDomainEventAsync(A<OfficevibeEvent>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    private sealed class FakeDomainEventHandler
    {
    }
}