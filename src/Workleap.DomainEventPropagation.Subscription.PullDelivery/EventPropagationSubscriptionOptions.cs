using Azure.Core;

namespace Workleap.DomainEventPropagation;

public class EventPropagationSubscriptionOptions
{
    internal const string DefaultSectionName = "EventPropagation:Subscription";

    public string TopicAccessKey { get; set; } = string.Empty;

    public TokenCredential? TokenCredential { get; set; }

    public string TopicEndpoint { get; set; } = string.Empty;

    public string TopicName { get; set; } = string.Empty;

    public string SubscriptionName { get; set; } = string.Empty;

    public int MaxDegreeOfParallelism { get; set; } = 1;

    /// <summary>
    /// Client side maximum retry count before sending the message to the dead-letter queue.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    public IReadOnlyCollection<TimeSpan>? RetryDelays { get; set; }

    /// <summary>
    /// Maximum number of events to pull from Event Grid per poll.
    /// </summary>
    public int MaxPullBatchSize { get; set; } = 100;

    /// <summary>
    /// Interval to wait between polling for new events.
    /// </summary>
    public TimeSpan PullInterval { get; set; } = TimeSpan.Zero;
}