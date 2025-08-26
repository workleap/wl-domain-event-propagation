using Microsoft.Extensions.Options;

namespace Workleap.DomainEventPropagation;

public class EventPropagationSubscriptionOptionsValidator : IValidateOptions<EventPropagationSubscriptionOptions>
{
    public ValidateOptionsResult Validate(string? name, EventPropagationSubscriptionOptions options)
    {
        if (options.TokenCredential is null && string.IsNullOrWhiteSpace(options.TopicAccessKey))
        {
            return ValidateOptionsResult.Fail("A token credential or an access key is required");
        }

        if (string.IsNullOrWhiteSpace(options.TopicEndpoint))
        {
            return ValidateOptionsResult.Fail("A topic endpoint is required");
        }

        if (!Uri.TryCreate(options.TopicEndpoint, UriKind.Absolute, out _))
        {
            return ValidateOptionsResult.Fail("The topic endpoint must be an absolute URI");
        }

        if (string.IsNullOrWhiteSpace(options.TopicName))
        {
            return ValidateOptionsResult.Fail("A topic endpoint is required");
        }

        if (string.IsNullOrWhiteSpace(options.SubscriptionName))
        {
            return ValidateOptionsResult.Fail("A topic endpoint is required");
        }

        if (options.MaxRetries is < 0 or > 10)
        {
            return ValidateOptionsResult.Fail("MaxRetries must be between 0 and 10. The upper limit ensures the event's time-to-live does not expire.");
        }

        return ValidateOptionsResult.Success;
    }
}