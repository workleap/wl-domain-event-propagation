using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Workleap.DomainEventPropagation;

public static class EventPropagationPublisherBuilderExtensions
{
    public static IEventPropagationPublisherBuilder AddApplicationInsights(this IEventPropagationPublisherBuilder builder)
    {
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPublishingDomainEventBehavior, ApplicationInsightsPublishingDomainEventBehavior>());

        return builder;
    }
}