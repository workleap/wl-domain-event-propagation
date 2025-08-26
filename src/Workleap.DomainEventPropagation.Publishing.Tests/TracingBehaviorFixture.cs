using Azure.Messaging.EventGrid;
using FakeItEasy;
using GSoft.Extensions.Xunit;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Workleap.DomainEventPropagation.Tests;

namespace Workleap.DomainEventPropagation.Publishing.Tests;

public class TracingBehaviorFixture : BaseUnitFixture
{
    public override IServiceCollection ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        // OpenTelemetry test dependencies
        services.AddSingleton<InMemoryActivityTracker>();

        // Application insights dependencies
        services.AddSingleton<TelemetryClient>();

        services.AddOptions<EventPropagationPublisherOptions>()
            .Configure(opt =>
            {
                opt.TopicName = "TopicName";
                opt.TopicEndpoint = "http://topic.entpoint";
                opt.TopicAccessKey = "AccessKey";
            });

        services.AddEventPropagationPublisher().AddApplicationInsights();
        services.AddSingleton(A.Fake<IAzureClientFactory<EventGridPublisherClient>>());

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPublishingDomainEventBehavior, ThrowingPublishingBehavior>());

        return services;
    }
}