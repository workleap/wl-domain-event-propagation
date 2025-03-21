using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Workleap.DomainEventPropagation.Publishing.Tests;

public class EventPropagationPublisherOptionsTests
{
    [Fact]
    public void GivenEventPropagationConfigurationAsIConfiguration_WhenLoadedProperly_ThenPropertiesMatch()
    {
        var myConfiguration = new Dictionary<string, string>
        {
            [$"{EventPropagationPublisherOptions.SectionName}:{nameof(EventPropagationPublisherOptions.TopicAccessKey)}"] = "accessKey",
            [$"{EventPropagationPublisherOptions.SectionName}:{nameof(EventPropagationPublisherOptions.TopicEndpoint)}"] = "http://topicurl.com",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(myConfiguration!)
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddEventPropagationPublisher();

        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<EventPropagationPublisherOptions>>().Value;

        Assert.Equal(myConfiguration[$"{EventPropagationPublisherOptions.SectionName}:{nameof(EventPropagationPublisherOptions.TopicEndpoint)}"], options.TopicEndpoint);
        Assert.Equal(myConfiguration[$"{EventPropagationPublisherOptions.SectionName}:{nameof(EventPropagationPublisherOptions.TopicAccessKey)}"], options.TopicAccessKey);
    }
}