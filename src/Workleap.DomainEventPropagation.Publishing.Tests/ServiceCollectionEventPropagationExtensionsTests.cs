using Azure.Identity;
using Azure.Messaging.EventGrid;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Workleap.DomainEventPropagation.Publishing.Tests;

public class ServiceCollectionEventPropagationExtensionsTests
{
#pragma warning disable CS0618 // Obsolete members used for backward compatibility testing

    [Fact]
    public void GivenEventPropagationConfigPresent_WhenAddEventPropagationPublisher_ThenOptionsAreSet()
    {
        // Given
        var inMemorySettings = new Dictionary<string, string?>
        {
            [$"{EventPropagationPublisherOptions.SectionName}:TopicEndpoint"] = "http://topicEndpoint.io",
            [$"{EventPropagationPublisherOptions.SectionName}:TopicAccessKey"] = "topicAccessKey",
        };

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        services.AddSingleton<IConfiguration>(configuration);

        // When
        services.AddEventPropagationPublisher();
        var serviceProvider = services.BuildServiceProvider();
        var publisherOptions = serviceProvider.GetRequiredService<IOptions<EventPropagationPublisherOptions>>().Value;

        // Then
        Assert.Equal("http://topicEndpoint.io", publisherOptions.TopicEndpoint);
        Assert.Equal("topicAccessKey", publisherOptions.TopicAccessKey);
    }

    [Fact]
    public void GivenEventPropagationConfigure_WhenAddEventPropagationPublisher_ThenOptionsAreSet()
    {
        // Given
        var inMemorySettings = new Dictionary<string, string?>
        {
            [$"{EventPropagationPublisherOptions.SectionName}:TopicEndpoint"] = "http://topicEndpoint.io",
            [$"{EventPropagationPublisherOptions.SectionName}:TopicAccessKey"] = "topicAccessKey",
        };

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        services.AddSingleton<IConfiguration>(configuration);

        // When
        services.AddEventPropagationPublisher(options =>
        {
            options.TopicEndpoint = "http://ovewrite.io";
        });
        var serviceProvider = services.BuildServiceProvider();
        var publisherOptions = serviceProvider.GetRequiredService<IOptions<EventPropagationPublisherOptions>>().Value;

        // Then
        Assert.Equal("http://ovewrite.io", publisherOptions.TopicEndpoint);
        Assert.Equal("topicAccessKey", publisherOptions.TopicAccessKey);
    }

    [Fact]
    public void GivenConfigWithAccessKey_WhenAddEventPropagationPublisher_ThenEventGridPublisherClientConfigured()
    {
        // Given
        var inMemorySettings = new Dictionary<string, string?>
        {
            [$"{EventPropagationPublisherOptions.SectionName}:TopicEndpoint"] = "http://topicEndpoint.io",
            [$"{EventPropagationPublisherOptions.SectionName}:TopicAccessKey"] = "topicAccessKey",
        };

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        services.AddSingleton<IConfiguration>(configuration);

        // When
        services.AddEventPropagationPublisher();
        var serviceProvider = services.BuildServiceProvider();
        var clientFactory = serviceProvider.GetRequiredService<IAzureClientFactory<EventGridPublisherClient>>();
        var client = clientFactory.CreateClient(EventPropagationPublisherOptions.EventGridClientName);

        // Then
        Assert.NotNull(client);
    }

    [Fact]
    public void GivenConfigWithTokenCredentials_WhenAddEventPropagationPublisher_ThenEventGridPublisherClientConfigured()
    {
        // Given
        var inMemorySettings = new Dictionary<string, string?>
        {
            [$"{EventPropagationPublisherOptions.SectionName}:TopicEndpoint"] = "http://topicEndpoint.io",
        };

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        services.AddSingleton<IConfiguration>(configuration);

        // When
        services.AddEventPropagationPublisher(options =>
        {
            options.TokenCredential = new AzureCliCredential();
        });
        var serviceProvider = services.BuildServiceProvider();
        var clientFactory = serviceProvider.GetRequiredService<IAzureClientFactory<EventGridPublisherClient>>();
        var client = clientFactory.CreateClient(EventPropagationPublisherOptions.EventGridClientName);

        // Then
        Assert.NotNull(client);
    }

    [Fact]
    public void GivenNullServiceCollection_WhenAddEventPropagationPublisher_ThenThrowsArgumentNullException()
    {
        // Given
        var services = (IServiceCollection?)null;

        // When
        var exception = Assert.Throws<ArgumentNullException>(() => services!.AddEventPropagationPublisher());

        // Then
        Assert.Equal("services", exception.ParamName);
    }

    [Fact]
    public void GivenNullConfigure_WhenAddEventPropagationPublisher_ThenThrowsArgumentNullException()
    {
        // Given
        var services = new ServiceCollection();
        Action<EventPropagationPublisherOptions>? configure = null;

        // When
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddEventPropagationPublisher(configure!));

        // Then
        Assert.Equal("configure", exception.ParamName);
    }

    [Fact]
    public void GivenExistingApi_WhenAddEventPropagationPublisher_ThenSingletonClientStillRegistered()
    {
        // Given
        var inMemorySettings = new Dictionary<string, string?>
        {
            [$"{EventPropagationPublisherOptions.SectionName}:TopicEndpoint"] = "http://topicEndpoint.io",
            [$"{EventPropagationPublisherOptions.SectionName}:TopicAccessKey"] = "topicAccessKey",
        };

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        services.AddSingleton<IConfiguration>(configuration);

        // When
        services.AddEventPropagationPublisher();
        var serviceProvider = services.BuildServiceProvider();

        // Then
        var client = serviceProvider.GetService<IEventPropagationClient>();
        Assert.NotNull(client);
    }

    [Fact]
    public void GivenNullTopicName_WhenAddTopic_ThenThrowsArgumentNullException()
    {
        // Given
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddSingleton<IConfiguration>(configuration);
        var builder = services.AddEventPropagationPublisher();

        // When
        var exception = Assert.Throws<ArgumentNullException>(() => builder.AddTopic(null!, _ => { }));

        // Then
        Assert.Equal("topicName", exception.ParamName);
    }

    [Fact]
    public void GivenNullConfigureOptions_WhenAddTopic_ThenThrowsArgumentNullException()
    {
        // Given
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddSingleton<IConfiguration>(configuration);
        var builder = services.AddEventPropagationPublisher();

        // When
        var exception = Assert.Throws<ArgumentNullException>(() => builder.AddTopic("Topic", null!));

        // Then
        Assert.Equal("configureOptions", exception.ParamName);
    }

#pragma warning restore CS0618

    [Fact]
    public void GivenAddTopic_WhenBuildServiceProvider_ThenNamedOptionsAreRegistered()
    {
        // Given
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddSingleton<IConfiguration>(configuration);

        // When
        services.AddEventPropagationPublishing()
            .AddTopic("TopicA", opts =>
            {
                opts.TopicEndpoint = "http://topicA.io";
                opts.TopicAccessKey = "keyA";
            })
            .AddTopic("TopicB", opts =>
            {
                opts.TopicEndpoint = "http://topicB.io";
                opts.TopicAccessKey = "keyB";
            });

        var serviceProvider = services.BuildServiceProvider();
        var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<EventPropagationPublisherOptions>>();

        // Then
        var topicAOptions = optionsMonitor.Get("TopicA");
        Assert.Equal("http://topicA.io", topicAOptions.TopicEndpoint);
        Assert.Equal("keyA", topicAOptions.TopicAccessKey);

        var topicBOptions = optionsMonitor.Get("TopicB");
        Assert.Equal("http://topicB.io", topicBOptions.TopicEndpoint);
        Assert.Equal("keyB", topicBOptions.TopicAccessKey);
    }

    [Fact]
    public void GivenAddTopic_WhenBuildServiceProvider_ThenClientFactoryIsRegistered()
    {
        // Given
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddSingleton<IConfiguration>(configuration);

        // When
        services.AddEventPropagationPublishing()
            .AddTopic("TopicA", opts =>
            {
                opts.TopicEndpoint = "http://topicA.io";
                opts.TopicAccessKey = "keyA";
            });

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetService<IEventPropagationClientFactory>();

        // Then
        Assert.NotNull(factory);
    }

    [Fact]
    public void GivenAddTopicWithConfigSection_WhenBuildServiceProvider_ThenNamedOptionsAreBoundFromConfig()
    {
        // Given
        var inMemorySettings = new Dictionary<string, string?>
        {
            [$"{EventPropagationPublisherOptions.SectionName}:TopicA:TopicEndpoint"] = "http://topicA.io",
            [$"{EventPropagationPublisherOptions.SectionName}:TopicA:TopicAccessKey"] = "keyA",
        };

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        services.AddSingleton<IConfiguration>(configuration);

        // When
        services.AddEventPropagationPublishing()
            .AddTopic("TopicA");

        var serviceProvider = services.BuildServiceProvider();
        var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<EventPropagationPublisherOptions>>();

        // Then
        var topicAOptions = optionsMonitor.Get("TopicA");
        Assert.Equal("http://topicA.io", topicAOptions.TopicEndpoint);
        Assert.Equal("keyA", topicAOptions.TopicAccessKey);
    }

    [Fact]
    public void GivenNewApi_WhenAddEventPropagationPublishing_ThenClientFactoryIsRegistered()
    {
        // Given
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddSingleton<IConfiguration>(configuration);

        // When
        services.AddEventPropagationPublishing()
            .AddTopic("TopicA", opts =>
            {
                opts.TopicEndpoint = "http://topicA.io";
                opts.TopicAccessKey = "keyA";
            });

        var serviceProvider = services.BuildServiceProvider();

        // Then
        var factory = serviceProvider.GetService<IEventPropagationClientFactory>();
        Assert.NotNull(factory);
    }

    [Fact]
    public void GivenNewApi_WhenAddEventPropagationPublishing_ThenSingletonClientIsNotRegistered()
    {
        // Given
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddSingleton<IConfiguration>(configuration);

        // When
        services.AddEventPropagationPublishing()
            .AddTopic("TopicA", opts =>
            {
                opts.TopicEndpoint = "http://topicA.io";
                opts.TopicAccessKey = "keyA";
            });

        var serviceProvider = services.BuildServiceProvider();

        // Then
        var client = serviceProvider.GetService<IEventPropagationClient>();
        Assert.Null(client);
    }

    [Fact]
    public void GivenNullServiceCollection_WhenAddEventPropagationPublishing_ThenThrowsArgumentNullException()
    {
        // Given
        var services = (IServiceCollection?)null;

        // When
        var exception = Assert.Throws<ArgumentNullException>(() => services!.AddEventPropagationPublishing());

        // Then
        Assert.Equal("services", exception.ParamName);
    }
}