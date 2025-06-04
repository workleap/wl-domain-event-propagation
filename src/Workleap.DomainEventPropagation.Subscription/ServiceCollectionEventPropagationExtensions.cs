using Microsoft.Extensions.Configuration;
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

        var builder = new EventPropagationSubscriberBuilder(services);

        var configuration = services.BuildServiceProvider().GetService<IConfiguration>();
        if (configuration != null)
        {
            var section = configuration.GetSection(EventPropagationThrottlingOptions.DefaultSectionName);
            if (section.Exists())
            {
                var options = new EventPropagationThrottlingOptions();
                section.Bind(options);

                services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<EventPropagationThrottlingOptions>, EventPropagationThrottlingOptionsValidator>());
                services.Configure<EventPropagationThrottlingOptions>(section);
                services.TryAddEnumerable(ServiceDescriptor.Singleton<ISubscriptionDomainEventBehavior, ThrottlingDomainEventBehavior>());
            }
        }

        return builder;
    }
}