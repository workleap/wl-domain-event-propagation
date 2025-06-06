using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Workleap.DomainEventPropagation;

public static class ServiceCollectionEventPropagationExtensions
{
    public static IEventPropagationSubscriberBuilder AddEventPropagationSubscriber(this IServiceCollection services)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        return new EventPropagationSubscriberBuilder(services);
    }

    public static IEventPropagationSubscriberBuilder AddRateLimiting(this IEventPropagationSubscriberBuilder builder, Action<EventPropagationThrottlingOptions>? configure = null)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ISubscriptionDomainEventBehavior, ThrottlingDomainEventBehavior>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<EventPropagationThrottlingOptions>, EventPropagationThrottlingOptionsValidator>());

        var optionsBuilder = builder.Services.AddOptions<EventPropagationThrottlingOptions>()
            .BindConfiguration(EventPropagationThrottlingOptions.DefaultSectionName)
            .ValidateOnStart();

        if (configure != null)
        {
            optionsBuilder.Configure(configure);
        }

        return builder;
    }
}