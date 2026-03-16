using Azure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Workleap.DomainEventPropagation.Subscription.PullDelivery.Tests;

public class DomainEventContextTests
{
    [Fact]
    public async Task GivenCloudEvent_WhenHandled_ThenContextIsAvailableInHandler()
    {
        // Given
        var services = new ServiceCollection();
        services
            .AddPullDeliverySubscription()
            .AddTopicSubscription()
            .AddDomainEventHandler<ContextCaptureDomainEvent, ContextCaptureDomainEventHandler>();
        services.AddSingleton<ILogger<ICloudEventHandler>, NullLogger<ICloudEventHandler>>();

        var sp = services.BuildServiceProvider();
        var handler = sp.GetRequiredService<ICloudEventHandler>();

        var wrapper = DomainEventWrapper.Wrap(new ContextCaptureDomainEvent());
        var cloudEvent = new CloudEvent(
            type: wrapper.DomainEventName,
            source: "http://test-source.com",
            jsonSerializableData: wrapper.Data);

        // When
        ContextCaptureDomainEventHandler.Reset();
        await handler.HandleCloudEventAsync(cloudEvent, CancellationToken.None);

        // Then
        Assert.NotNull(ContextCaptureDomainEventHandler.CapturedContext);
        Assert.Equal(cloudEvent.Id, ContextCaptureDomainEventHandler.CapturedContext!.Id);
        Assert.Equal("http://test-source.com", ContextCaptureDomainEventHandler.CapturedContext.Source);
        Assert.Equal("context-capture-cloud-event", ContextCaptureDomainEventHandler.CapturedContext.DomainEventName);
        Assert.Equal(EventSchema.CloudEvent, ContextCaptureDomainEventHandler.CapturedContext.EventSchema);
    }

    [Fact]
    public async Task GivenCloudEvent_WhenHandled_ThenContextIsClearedAfterPipeline()
    {
        // Given
        var services = new ServiceCollection();
        services
            .AddPullDeliverySubscription()
            .AddTopicSubscription()
            .AddDomainEventHandler<ContextCaptureDomainEvent, ContextCaptureDomainEventHandler>();
        services.AddSingleton<ILogger<ICloudEventHandler>, NullLogger<ICloudEventHandler>>();

        var sp = services.BuildServiceProvider();
        var handler = sp.GetRequiredService<ICloudEventHandler>();

        var wrapper = DomainEventWrapper.Wrap(new ContextCaptureDomainEvent());
        var cloudEvent = new CloudEvent(
            type: wrapper.DomainEventName,
            source: "http://test-source.com",
            jsonSerializableData: wrapper.Data);

        // When
        ContextCaptureDomainEventHandler.Reset();
        await handler.HandleCloudEventAsync(cloudEvent, CancellationToken.None);

        // Then — context should not be available outside the pipeline
        Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<IDomainEventContext>());
    }

    [Fact]
    public void GivenNoEventBeingProcessed_WhenResolvingContext_ThenThrowsInvalidOperationException()
    {
        // Given
        var services = new ServiceCollection();
        services.AddPullDeliverySubscription();
        var sp = services.BuildServiceProvider();

        // When / Then
        Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<IDomainEventContext>());
    }

    [Fact]
    public void GivenBehaviorRegistration_WhenResolvingBehaviors_ThenContextBehaviorIsBeforeTracing()
    {
        // Given
        var services = new ServiceCollection();
        services.AddPullDeliverySubscription();
        var sp = services.BuildServiceProvider();

        // When
        var behaviors = sp.GetServices<ISubscriptionDomainEventBehavior>().ToArray();

        // Then
        Assert.IsType<DomainEventContextBehavior>(behaviors[0]);
        Assert.IsType<TracingSubscriptionDomainEventBehavior>(behaviors[1]);
    }
}

[DomainEvent("context-capture-cloud-event", EventSchema.CloudEvent)]
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