using Azure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Workleap.DomainEventPropagation.Subscription.PullDelivery.Tests.Events;
using Workleap.DomainEventPropagation.Tests;

namespace Workleap.DomainEventPropagation.Subscription.PullDelivery.Tests;

[Collection("Telemetry")]
public class CloudEventHandlerTracingTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(4, true)]
    public async Task GivenFailingEventHandler_WhenHandleCloudEventAsync_ThenActivityHasDeliveryTags(int deliveryCount, bool isLastDelivery)
    {
        // Given
        using var activityTracker = new InMemoryActivityTracker();
        var cloudEvent = GivenCloudEvent(new SampleThatCausesExceptionDomainEvent { Message = "A message" });
        var services = new ServiceCollection();
        services
            .AddPullDeliverySubscription()
            .AddTopicSubscription()
            .AddDomainEventHandler<SampleThatCausesExceptionDomainEvent, SampleThatCausesExceptionDomainEventHandler>();
        var handler = GivenCloudEventHandler(services);

        // When
        await Assert.ThrowsAnyAsync<Exception>(() => handler.HandleCloudEventAsync(cloudEvent, deliveryCount, isLastDelivery, CancellationToken.None));

        // Then
        activityTracker.AssertDeliveryTagsSet(TracingHelper.GetCloudEventsSubscriberActivityName(cloudEvent.Type), deliveryCount, isLastDelivery);
    }

    [Fact]
    public async Task GivenNoHandlerRegisteredForEvent_WhenHandleCloudEventAsync_ThenActivityIsTaggedAsLastDelivery()
    {
        // Given
        using var activityTracker = new InMemoryActivityTracker();
        var cloudEvent = GivenCloudEvent(new SampleEvent { Message = "A message" });
        var services = new ServiceCollection();
        services
            .AddPullDeliverySubscription()
            .AddTopicSubscription()
            .AddDomainEventHandler<SampleEvent, SampleEventTestHandler>();
        services.RemoveAll<IDomainEventHandler<SampleEvent>>();
        var handler = GivenCloudEventHandler(services);

        // When
        await Assert.ThrowsAsync<DomainEventHandlerNotRegisteredException>(() => handler.HandleCloudEventAsync(cloudEvent, deliveryCount: 1, isLastDelivery: false, CancellationToken.None));

        // Then
        activityTracker.AssertDeliveryTagsSet(TracingHelper.GetCloudEventsSubscriberActivityName(cloudEvent.Type), deliveryCount: 1, isLastDelivery: true);
    }

    private static CloudEvent GivenCloudEvent<T>(T domainEvent)
        where T : IDomainEvent
    {
        var wrapper = DomainEventWrapper.Wrap(domainEvent);
        return new CloudEvent(type: wrapper.DomainEventName, source: "http://source.com", jsonSerializableData: wrapper.Data);
    }

    private static ICloudEventHandler GivenCloudEventHandler(IServiceCollection services)
    {
        return services.BuildServiceProvider().GetRequiredService<ICloudEventHandler>();
    }
}