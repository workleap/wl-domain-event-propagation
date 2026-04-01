using Azure.Messaging.EventGrid;
using FakeItEasy;
using GSoft.Extensions.Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Workleap.DomainEventPropagation.Tests;

namespace Workleap.DomainEventPropagation.Subscription.Tests;

[Collection(XunitCollectionConstants.StaticActivitySensitive)]
public sealed class TracingBehaviorTests : BaseUnitTest<TracingBehaviorFixture>
{
    private readonly InMemoryActivityTracker _activities;

    public TracingBehaviorTests(TracingBehaviorFixture fixture, ITestOutputHelper testOutputHelper)
        : base(fixture, testOutputHelper)
    {
        this._activities = this.Services.GetRequiredService<InMemoryActivityTracker>();
    }

    [Fact]
    public async Task GivenActivityListener_WhenHandleEventGridEvent_ThenHandleWithTracing()
    {
        var wrapperEvent = DomainEventWrapper.Wrap(new SampleDomainEvent("Hello world"));

        wrapperEvent.SetMetadata("traceparent", "00-0af7651916cd43dd8448eb211c80319c-00f067aa0ba902b7-01");
        var eventGridEvent = new EventGridEvent("subject", wrapperEvent.DomainEventName, "version", wrapperEvent.ToBinaryData());
        var eventSubscriptionContext = new DomainEventSubscriptionContext();

        var behaviors = this.Services.GetServices<ISubscriptionDomainEventBehavior>();
        var domainEventGridWebhookHandler = new DomainEventGridWebhookHandler(this.Services, A.Fake<IDomainEventTypeRegistry>(), NullLogger<DomainEventGridWebhookHandler>.Instance, behaviors);
        await domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(eventGridEvent, eventSubscriptionContext, CancellationToken.None);

        var activityName = TracingHelper.GetEventGridEventsSubscriberActivityName(wrapperEvent.DomainEventName);
        this._activities.AssertSubscribeSuccessful(activityName);
    }

    [Fact]
    public async Task GivenActivityListener_WhenHandleEventGridEventFails_ThenThrowsWithTracing()
    {
        var wrapperEvent = DomainEventWrapper.Wrap(new ThrowingDomainEvent());

        var eventGridEvent = new EventGridEvent("subject", wrapperEvent.DomainEventName, "version", BinaryData.FromObjectAsJson(wrapperEvent));
        var eventSubscriptionContext = new DomainEventSubscriptionContext();

        var behaviors = this.Services.GetServices<ISubscriptionDomainEventBehavior>();
        var domainEventGridWebhookHandler = new DomainEventGridWebhookHandler(this.Services, A.Fake<IDomainEventTypeRegistry>(), NullLogger<DomainEventGridWebhookHandler>.Instance, behaviors);

        var exception = await Assert.ThrowsAsync<Exception>(async () => await domainEventGridWebhookHandler.HandleEventGridWebhookEventAsync(eventGridEvent, eventSubscriptionContext, CancellationToken.None));

        var activityName = TracingHelper.GetEventGridEventsSubscriberActivityName(wrapperEvent.DomainEventName);
        this._activities.AssertSubscriptionFailed(activityName, exception);
    }

    [Fact]
    public async Task GivenTracingBehaviors_WhenRegisterBehaviors_ThenRegisteredInRightOrder()
    {
        var subscriptionBehaviors = this.Services.GetServices<ISubscriptionDomainEventBehavior>().ToArray();

        Assert.IsType<TracingSubscriptionDomainEventBehavior>(subscriptionBehaviors[0]);
        Assert.IsType<ApplicationInsightsSubscriptionDomainEventBehavior>(subscriptionBehaviors[1]);
    }
}