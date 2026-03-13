using Microsoft.Extensions.DependencyInjection;

namespace Workleap.DomainEventPropagation;

public static class ServiceCollectionEventPropagationExtensions
{
    public static IEventPropagationPublisherBuilder AddEventPropagationPublishing(this IServiceCollection services)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        return new EventPropagationPublisherBuilder(services);
    }

    [Obsolete("Use AddEventPropagationPublishing().AddTopic(...) instead.")]
    public static IEventPropagationPublisherBuilder AddEventPropagationPublisher(this IServiceCollection services)
        => services.AddEventPropagationPublisher(_ => { });

    [Obsolete("Use AddEventPropagationPublishing().AddTopic(...) instead.")]
    public static IEventPropagationPublisherBuilder AddEventPropagationPublisher(this IServiceCollection services, Action<EventPropagationPublisherOptions> configure)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        return new EventPropagationPublisherBuilder(services, configure);
    }
}