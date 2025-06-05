using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Workleap.DomainEventPropagation.Subscription.Tests;

public class ThrottlingDomainEventBehaviorTests
{
    private readonly DomainEventHandlerDelegate _next = A.Fake<DomainEventHandlerDelegate>();

    [Fact]
    public async Task GivenEventsUnderRateLimit_WhenHandlingEvents_ThenAllEventsProcessed()
    {
        var options = Options.Create(new EventPropagationThrottlingOptions
        {
            MaxEventsPerSecond = 5,
            QueueLimit = 100
        });

        var throttlingBehavior = new ThrottlingDomainEventBehavior(options);
        var domainEvent = DomainEventWrapper.Wrap(new TestDomainEvent { Number = 1, Text = "Test" });

        for (var i = 0; i < 5; i++)
        {
            await throttlingBehavior.HandleAsync(domainEvent, this._next, CancellationToken.None);
        }

        A.CallTo(() => this._next(A<DomainEventWrapper>._, A<CancellationToken>._))
            .MustHaveHappened(5, Times.Exactly);
    }

    [Fact]
    public async Task GivenConcurrentEventsOverRateLimit_WhenHandlingEvents_ThenExcessEventsThrottled()
    {
        var options = Options.Create(new EventPropagationThrottlingOptions
        {
            MaxEventsPerSecond = 1,
            QueueLimit = 1
        });

        var throttlingBehavior = new ThrottlingDomainEventBehavior(options);
        var domainEvent = DomainEventWrapper.Wrap(new TestDomainEvent { Number = 1, Text = "Test" });

        // Send 3 events concurrently (1 processed, 1 queued, 1 rejected)
        var tasks = new List<Task>();
        var exceptions = new List<Exception>();

        for (var i = 0; i < 3; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await throttlingBehavior.HandleAsync(domainEvent, this._next, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Single(exceptions);
        Assert.IsType<ThrottlingException>(exceptions[0]);

        // Only 2 events should be processed (1 immediate + 1 queued)
        A.CallTo(() => this._next(A<DomainEventWrapper>._, A<CancellationToken>._))
            .MustHaveHappened(2, Times.Exactly);
    }

    [Fact]
    public async Task GivenEventsOverRateLimit_WhenHandlingEvents_ThenExcessEventsThrottled()
    {
        var options = Options.Create(new EventPropagationThrottlingOptions
        {
            MaxEventsPerSecond = 1,
            QueueLimit = 0 // No queuing allowed
        });

        var throttlingBehavior = new ThrottlingDomainEventBehavior(options);
        var domainEvent = DomainEventWrapper.Wrap(new TestDomainEvent { Number = 1, Text = "Test" });

        await throttlingBehavior.HandleAsync(domainEvent, this._next, CancellationToken.None);
        await Assert.ThrowsAsync<ThrottlingException>(async () =>
            await throttlingBehavior.HandleAsync(domainEvent, this._next, CancellationToken.None));

        A.CallTo(() => this._next(A<DomainEventWrapper>._, A<CancellationToken>._))
            .MustHaveHappened(1, Times.Exactly);
    }

    [Fact]
    public async Task GivenQueuedEvents_WhenHandlingEvents_ThenQueuedEventsProcessedAfterDelay()
    {
        var options = Options.Create(new EventPropagationThrottlingOptions
        {
            MaxEventsPerSecond = 2,
            QueueLimit = 2
        });

        var throttlingBehavior = new ThrottlingDomainEventBehavior(options);
        var domainEvent = DomainEventWrapper.Wrap(new TestDomainEvent { Number = 1, Text = "Test" });
        var tasks = new List<Task>();
        for (var i = 0; i < 4; i++)
        {
            tasks.Add(throttlingBehavior.HandleAsync(domainEvent, this._next, CancellationToken.None));
        }

        await Task.WhenAll(tasks);

        A.CallTo(() => this._next(A<DomainEventWrapper>._, A<CancellationToken>._))
            .MustHaveHappened(4, Times.Exactly);
    }

    [DomainEvent("test")]
    private class TestDomainEvent : IDomainEvent
    {
        public string Text { get; set; } = string.Empty;
        public int Number { get; set; }
    }
}