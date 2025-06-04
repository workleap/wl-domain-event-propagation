using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Workleap.DomainEventPropagation;

internal class ThrottlingDomainEventBehavior : ISubscriptionDomainEventBehavior
{
    private readonly ILogger<ThrottlingDomainEventBehavior> _logger;
    private readonly RateLimiter _rateLimiter;

    internal ThrottlingDomainEventBehavior(IOptions<EventPropagationThrottlingOptions> options, ILogger<ThrottlingDomainEventBehavior> logger)
    {
        this._logger = logger;
        this._rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = options.Value.MaxEventsPerSecond,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = options.Value.QueueLimit,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = options.Value.MaxEventsPerSecond,
            AutoReplenishment = true
        });
    }

    public async Task HandleAsync(DomainEventWrapper domainEvent, DomainEventHandlerDelegate next, CancellationToken cancellationToken)
    {
        using var lease = await this._rateLimiter.AcquireAsync(1, cancellationToken).ConfigureAwait(false);

        if (lease.IsAcquired)
        {
            await next(domainEvent, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            this._logger.LogWarning("Throttling event {EventName} due to rate limiting.", domainEvent.DomainEventName);

            throw new ThrottlingException();
        }
    }
}