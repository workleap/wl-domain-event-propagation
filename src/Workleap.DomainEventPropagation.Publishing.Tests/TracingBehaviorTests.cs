using GSoft.Extensions.Xunit;
using Microsoft.Extensions.DependencyInjection;
using Workleap.DomainEventPropagation.Tests;

namespace Workleap.DomainEventPropagation.Publishing.Tests;

public sealed class TracingBehaviorTests : BaseUnitTest<TracingBehaviorFixture>
{
    private readonly InMemoryActivityTracker _activities;
    private readonly IEventPropagationClient _eventPropagationClient;

    public TracingBehaviorTests(TracingBehaviorFixture fixture, ITestOutputHelper testOutputHelper)
        : base(fixture, testOutputHelper)
    {
        this._eventPropagationClient = this.Services.GetRequiredService<IEventPropagationClient>();
        this._activities = this.Services.GetRequiredService<InMemoryActivityTracker>();
    }

    [Fact]
    public async Task GivenActivityListener_WhenPublishDomainEvent_ThenHandleWithTracing()
    {
        var domainEvent = new SampleDomainEvent();
        await this._eventPropagationClient.PublishDomainEventAsync(domainEvent, CancellationToken.None);

        var activityName = TracingHelper.GetEventGridEventsPublisherActivityName("sample-event");
        this._activities.AssertPublishSuccessful(activityName);
    }

    [Fact]
    public async Task GivenActivityListener_WhenPublishDomainEventFails_ThenThrowsWithTracing()
    {
        var domainEvent = new ThrowingDomainEvent();
        var exception = await Assert.ThrowsAsync<EventPropagationPublishingException>(async () =>
            await this._eventPropagationClient.PublishDomainEventAsync(domainEvent, CancellationToken.None));

        var activityName = TracingHelper.GetEventGridEventsPublisherActivityName(nameof(ThrowingDomainEvent));
        this._activities.AssertPublishFailed(activityName, exception.InnerException!);
    }

    [Fact]
    public async Task GivenTracingBehaviors_WhenRegisterBehaviors_ThenRegisteredInRightOrder()
    {
        var publishingBehaviors = this.Services.GetServices<IPublishingDomainEventBehavior>().ToArray();

        Assert.IsType<TracingPublishingDomainEventBehavior>(publishingBehaviors[0]);
        Assert.IsType<ApplicationInsightsPublishingDomainEventBehavior>(publishingBehaviors[1]);
    }
}