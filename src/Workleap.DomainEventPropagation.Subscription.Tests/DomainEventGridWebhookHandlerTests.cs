using System.Reflection;
using Azure.Messaging.EventGrid;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Workleap.DomainEventPropagation.Subscription.Tests.Mocks;

namespace Workleap.DomainEventPropagation.Subscription.Tests;

public class DomainEventGridWebhookHandlerTests
{
    private static readonly DomainEventWrapper DomainEvent = DomainEventWrapper.Wrap(new TestDomainEvent() { Number = 1, Text = "Hello world" });

    [Fact]
    public async Task GivenDomainEventIsFired_WhenThereIsNoDomainEventHandler_ThenDomainEventIsIgnored()
    {
        var services = new ServiceCollection();
        var eventProcessingBuilder = services.AddEventPropagationSubscriber();
        eventProcessingBuilder.AddDomainEventHandlers(typeof(DomainEventGridWebhookHandlerTests).Assembly);

        // No eventHandler is registered
        var domainEventHandler = A.Fake<IDomainEventHandler<TestDomainEvent>>();

        var domainEvent = new EventGridEvent("subject", DomainEvent.GetType().FullName, "version", BinaryData.FromObjectAsJson(DomainEvent));
        var domainEventGridWebhookHandler = new DomainEventGridWebhookHandler(services.BuildServiceProvider(), A.Fake<IDomainEventTypeRegistry>(), NullLogger<DomainEventGridWebhookHandler>.Instance, Array.Empty<ISubscriptionDomainEventBehavior>());
        await domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(domainEvent, CancellationToken.None);

        A.CallTo(() => domainEventHandler.HandleDomainEventAsync(A<TestDomainEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task GivenDomainEventIsFired_WhenThereIsADomainEventHandler_ThenDomainEventHandlerIsCalled()
    {
        var services = new ServiceCollection();
        var eventProcessingBuilder = services.AddEventPropagationSubscriber();
        eventProcessingBuilder.AddDomainEventHandlers(typeof(DomainEventGridWebhookHandlerTests).Assembly);

        // Given
        var domainEventHandler = A.Fake<IDomainEventHandler<TestDomainEvent>>();
        services.AddSingleton(domainEventHandler);

        var domainEvent = new EventGridEvent("subject", DomainEvent.GetType().FullName, "version", BinaryData.FromObjectAsJson(DomainEvent));

        var serviceProvider = services.BuildServiceProvider();
        var behaviors = serviceProvider.GetServices<ISubscriptionDomainEventBehavior>();
        var domainEventGridWebhookHandler = new DomainEventGridWebhookHandler(serviceProvider, A.Fake<IDomainEventTypeRegistry>(), NullLogger<DomainEventGridWebhookHandler>.Instance,  behaviors);
        await domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(domainEvent, CancellationToken.None);

        A.CallTo(() => domainEventHandler.HandleDomainEventAsync(A<TestDomainEvent>._, CancellationToken.None)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GivenDomainEventIsFired_WhenExceptionOccurs_ThenExceptionIsThrown()
    {
        var services = new ServiceCollection();
        var eventProcessingBuilder = services.AddEventPropagationSubscriber();
        eventProcessingBuilder.AddDomainEventHandlers(typeof(DomainEventGridWebhookHandlerTests).Assembly);

        // Given
        var domainEventHandler = A.Fake<IDomainEventHandler<TestDomainEvent>>();
        A.CallTo(() => domainEventHandler.HandleDomainEventAsync(A<TestDomainEvent>._, A<CancellationToken>._)).Throws(new Exception("Test exception"));
        services.AddSingleton(domainEventHandler);

        var serviceProvider = services.BuildServiceProvider();
        var behaviors = serviceProvider.GetServices<ISubscriptionDomainEventBehavior>();
        var domainEventGridWebhookHandler = new DomainEventGridWebhookHandler(serviceProvider, A.Fake<IDomainEventTypeRegistry>(), NullLogger<DomainEventGridWebhookHandler>.Instance, behaviors);

        await Assert.ThrowsAsync<TargetInvocationException>(() =>
        {
            var domainEvent = new EventGridEvent("subject", DomainEvent.GetType().FullName, "version", BinaryData.FromObjectAsJson(DomainEvent));
            return domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(domainEvent, CancellationToken.None);
        });

        A.CallTo(() => domainEventHandler.HandleDomainEventAsync(A<TestDomainEvent>._, CancellationToken.None)).MustHaveHappenedOnceExactly();
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
    public async Task GivenRegisteredTracingBehavior_WhenHandleEventGridWebhookEventAsync_ThenBehaviorCalled()
    {
        // Given
        var eventGridEvent = new EventGridEvent("Subject", DomainEvent.GetType().FullName, "1.0", new BinaryData(DomainEvent));

        var subscriberBehavior = A.Fake<ISubscriptionDomainEventBehavior>();
        var eventHandler = A.Fake<IDomainEventHandler<TestDomainEvent>>();

        var services = new ServiceCollection();
        services.AddSingleton(subscriberBehavior);
        services.AddSingleton(eventHandler);
        var serviceProvider = services.BuildServiceProvider();

        // When
        var webhookHandler = new DomainEventGridWebhookHandler(serviceProvider, A.Fake<IDomainEventTypeRegistry>(), NullLogger<DomainEventGridWebhookHandler>.Instance, new[] { subscriberBehavior });

        await webhookHandler.HandleEventGridWebhookEventAsync(eventGridEvent, CancellationToken.None);

        // Then
        A.CallTo(() => subscriberBehavior.HandleAsync(A<DomainEventWrapper>._, A<DomainEventHandlerDelegate>._, A<CancellationToken>._)).MustHaveHappened();
    }
}