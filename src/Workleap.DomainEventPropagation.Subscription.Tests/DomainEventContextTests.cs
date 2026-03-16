using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Workleap.DomainEventPropagation.Subscription.Tests;

public class DomainEventContextTests
{
    [Fact]
    public async Task GivenEventGridEvent_WhenHandled_ThenContextIsAvailableInHandler()
    {
        // Given
        var services = new ServiceCollection();
        services
            .AddEventPropagationSubscriber()
            .AddDomainEventHandler<ContextCaptureDomainEvent, ContextCaptureDomainEventHandler>();

        var sp = services.BuildServiceProvider();

        var wrapperEvent = DomainEventWrapper.Wrap(new ContextCaptureDomainEvent());
        var eventGridEvent = new EventGridEvent("subject", wrapperEvent.DomainEventName, "1.0", wrapperEvent.ToBinaryData());

        var behaviors = sp.GetServices<ISubscriptionDomainEventBehavior>();
        var handler = new DomainEventGridWebhookHandler(
            sp,
            sp.GetRequiredService<IDomainEventTypeRegistry>(),
            NullLogger<DomainEventGridWebhookHandler>.Instance,
            behaviors);

        // When
        ContextCaptureDomainEventHandler.Reset();
        await handler.HandleEventGridWebhookEventAsync(eventGridEvent, CancellationToken.None);

        // Then
        Assert.NotNull(ContextCaptureDomainEventHandler.CapturedContext);
        Assert.Equal(eventGridEvent.Id, ContextCaptureDomainEventHandler.CapturedContext!.Id);
        Assert.Equal(eventGridEvent.Topic, ContextCaptureDomainEventHandler.CapturedContext.Source);
        Assert.Equal("context-capture-event", ContextCaptureDomainEventHandler.CapturedContext.DomainEventName);
        Assert.Equal(EventSchema.EventGridEvent, ContextCaptureDomainEventHandler.CapturedContext.EventSchema);
    }

    [Fact]
    public async Task GivenCloudEvent_WhenHandled_ThenContextIsAvailableInHandler()
    {
        // Given
        var services = new ServiceCollection();
        services
            .AddEventPropagationSubscriber()
            .AddDomainEventHandler<ContextCaptureDomainEvent, ContextCaptureDomainEventHandler>();

        var sp = services.BuildServiceProvider();

        var wrapperEvent = DomainEventWrapper.Wrap(new ContextCaptureDomainEvent());
        var cloudEvent = new CloudEvent("http://test-source.com", wrapperEvent.DomainEventName, wrapperEvent.ToBinaryData(), "application/json");

        var behaviors = sp.GetServices<ISubscriptionDomainEventBehavior>();
        var handler = new DomainEventGridWebhookHandler(
            sp,
            sp.GetRequiredService<IDomainEventTypeRegistry>(),
            NullLogger<DomainEventGridWebhookHandler>.Instance,
            behaviors);

        // When
        ContextCaptureDomainEventHandler.Reset();
        await handler.HandleEventGridWebhookEventAsync(cloudEvent, CancellationToken.None);

        // Then
        Assert.NotNull(ContextCaptureDomainEventHandler.CapturedContext);
        Assert.Equal(cloudEvent.Id, ContextCaptureDomainEventHandler.CapturedContext!.Id);
        Assert.Equal("http://test-source.com", ContextCaptureDomainEventHandler.CapturedContext.Source);
        Assert.Equal("context-capture-event", ContextCaptureDomainEventHandler.CapturedContext.DomainEventName);
        Assert.Equal(EventSchema.CloudEvent, ContextCaptureDomainEventHandler.CapturedContext.EventSchema);
    }

    [Fact]
    public async Task GivenEvent_WhenHandled_ThenContextIsClearedAfterPipeline()
    {
        // Given
        var services = new ServiceCollection();
        services
            .AddEventPropagationSubscriber()
            .AddDomainEventHandler<ContextCaptureDomainEvent, ContextCaptureDomainEventHandler>();

        var sp = services.BuildServiceProvider();

        var wrapperEvent = DomainEventWrapper.Wrap(new ContextCaptureDomainEvent());
        var eventGridEvent = new EventGridEvent("subject", wrapperEvent.DomainEventName, "1.0", wrapperEvent.ToBinaryData());

        var behaviors = sp.GetServices<ISubscriptionDomainEventBehavior>();
        var handler = new DomainEventGridWebhookHandler(
            sp,
            sp.GetRequiredService<IDomainEventTypeRegistry>(),
            NullLogger<DomainEventGridWebhookHandler>.Instance,
            behaviors);

        // When
        ContextCaptureDomainEventHandler.Reset();
        await handler.HandleEventGridWebhookEventAsync(eventGridEvent, CancellationToken.None);

        // Then — context should not be available outside the pipeline
        Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<IDomainEventContext>());
    }

    [Fact]
    public void GivenNoEventBeingProcessed_WhenResolvingContext_ThenThrowsInvalidOperationException()
    {
        // Given
        var services = new ServiceCollection();
        services.AddEventPropagationSubscriber();
        var sp = services.BuildServiceProvider();

        // When / Then
        Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<IDomainEventContext>());
    }

    [Fact]
    public async Task GivenBehaviorRegistration_WhenResolvingBehaviors_ThenContextBehaviorIsBeforeTracing()
    {
        // Given
        var services = new ServiceCollection();
        services.AddEventPropagationSubscriber();
        var sp = services.BuildServiceProvider();

        // When
        var behaviors = sp.GetServices<ISubscriptionDomainEventBehavior>().ToArray();

        // Then
        Assert.IsType<DomainEventContextBehavior>(behaviors[0]);
        Assert.IsType<TracingSubscriptionDomainEventBehavior>(behaviors[1]);

        await Task.CompletedTask;
    }
}

[DomainEvent("context-capture-event")]
public sealed class ContextCaptureDomainEvent : IDomainEvent;

public sealed class ContextCaptureDomainEventHandler : IDomainEventHandler<ContextCaptureDomainEvent>
{
    private readonly IDomainEventContext _context;

    public static IDomainEventContext? CapturedContext { get; private set; }

    public ContextCaptureDomainEventHandler(IDomainEventContext context)
    {
        this._context = context;
    }

    public static void Reset() => CapturedContext = null;

    public Task HandleDomainEventAsync(ContextCaptureDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        CapturedContext = this._context;
        return Task.CompletedTask;
    }
}