using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Workleap.DomainEventPropagation;

public static class EventPropagationSubscriberBuilderExtensions
{
    public static IEventPropagationSubscriberBuilder AddApplicationInsights(this IEventPropagationSubscriberBuilder builder)
    {
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ISubscriptionDomainEventBehavior, ApplicationInsightsSubscriptionDomainEventBehavior>());

        return builder;
    }
}