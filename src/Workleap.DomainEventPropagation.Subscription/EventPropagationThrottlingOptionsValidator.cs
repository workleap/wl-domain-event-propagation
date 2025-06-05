using Microsoft.Extensions.Options;

namespace Workleap.DomainEventPropagation;

internal sealed class EventPropagationThrottlingOptionsValidator : IValidateOptions<EventPropagationThrottlingOptions>
{
    public ValidateOptionsResult Validate(string? name, EventPropagationThrottlingOptions options)
    {
        if (options.MaxEventsPerSecond <= 0)
        {
            return ValidateOptionsResult.Fail($"{nameof(EventPropagationThrottlingOptions.MaxEventsPerSecond)} must be a positive value");
        }

        if (options.QueueLimit <= 0)
        {
            return ValidateOptionsResult.Fail($"{nameof(EventPropagationThrottlingOptions.QueueLimit)} must be a positive value");
        }

        return ValidateOptionsResult.Success;
    }
}