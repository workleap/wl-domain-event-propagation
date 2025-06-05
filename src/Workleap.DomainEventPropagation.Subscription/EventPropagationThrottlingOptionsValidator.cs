using Microsoft.Extensions.Options;

namespace Workleap.DomainEventPropagation;

internal sealed class EventPropagationThrottlingOptionsValidator : IValidateOptions<EventPropagationThrottlingOptions>
{
    public ValidateOptionsResult Validate(string? name, EventPropagationThrottlingOptions options)
    {
        if (options.MaxEventsPerSecond <= 0)
        {
            return ValidateOptionsResult.Fail("MaxEventsPerSecond must be a positive value");
        }

        if (options.QueueLimit <= 0)
        {
            return ValidateOptionsResult.Fail("QueueLimit must be a positive value");
        }

        return ValidateOptionsResult.Success;
    }
}