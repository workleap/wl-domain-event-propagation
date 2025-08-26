using GSoft.Extensions.Xunit;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Workleap.DomainEventPropagation.Tests;

namespace Workleap.DomainEventPropagation.Subscription.Tests;

public class TracingBehaviorFixture : BaseUnitFixture
{
    public override IServiceCollection ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        // OpenTelemetry test dependencies
        services.AddSingleton<InMemoryActivityTracker>();

        // Application insights dependencies
        services.AddSingleton<TelemetryClient>();

        services.AddEventPropagationSubscriber()
            .AddApplicationInsights()
            .AddDomainEventHandler<SampleDomainEvent, SampleDomainEventHandler>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISubscriptionDomainEventBehavior, ThrowingSubscriptionBehavior>());

        return services;
    }
}