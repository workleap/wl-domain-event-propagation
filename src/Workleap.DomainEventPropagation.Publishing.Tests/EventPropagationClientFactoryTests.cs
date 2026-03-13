using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Workleap.DomainEventPropagation.Publishing.Tests;

#pragma warning disable CS0618 // Obsolete members used for backward compatibility testing

public class EventPropagationClientFactoryTests
{
    [Fact]
    public void GivenNamedTopic_WhenCreateClient_ThenReturnsWorkingClient()
    {
        // Given
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddEventPropagationPublisher()
            .AddTopic("TopicA", opts =>
            {
                opts.TopicEndpoint = "http://topicA.io";
                opts.TopicAccessKey = "keyA";
            });

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IEventPropagationClientFactory>();

        // When
        var client = factory.CreateClient("TopicA");

        // Then
        Assert.NotNull(client);
    }

    [Fact]
    public void GivenSameName_WhenCreateClientCalledTwice_ThenReturnsSameInstance()
    {
        // Given
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddEventPropagationPublisher()
            .AddTopic("TopicA", opts =>
            {
                opts.TopicEndpoint = "http://topicA.io";
                opts.TopicAccessKey = "keyA";
            });

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IEventPropagationClientFactory>();

        // When
        var client1 = factory.CreateClient("TopicA");
        var client2 = factory.CreateClient("TopicA");

        // Then
        Assert.Same(client1, client2);
    }

    [Fact]
    public void GivenDifferentNames_WhenCreateClient_ThenReturnsDifferentInstances()
    {
        // Given
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddEventPropagationPublisher()
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
        var factory = serviceProvider.GetRequiredService<IEventPropagationClientFactory>();

        // When
        var clientA = factory.CreateClient("TopicA");
        var clientB = factory.CreateClient("TopicB");

        // Then
        Assert.NotSame(clientA, clientB);
    }

    [Fact]
    public void GivenNamespaceTopic_WhenCreateClient_ThenReturnsWorkingClient()
    {
        // Given
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddEventPropagationPublisher()
            .AddTopic("NamespaceTopic", opts =>
            {
                opts.TopicType = TopicType.Namespace;
                opts.TopicEndpoint = "http://namespace.io";
                opts.TopicName = "orders";
                opts.TopicAccessKey = "keyNs";
            });

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IEventPropagationClientFactory>();

        // When
        var client = factory.CreateClient("NamespaceTopic");

        // Then
        Assert.NotNull(client);
    }

    [Fact]
    public void GivenMixedTopicTypes_WhenCreateClients_ThenEachReturnsCorrectType()
    {
        // Given
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddEventPropagationPublisher()
            .AddTopic("CustomTopic", opts =>
            {
                opts.TopicType = TopicType.Custom;
                opts.TopicEndpoint = "http://custom.io";
                opts.TopicAccessKey = "keyCustom";
            })
            .AddTopic("NamespaceTopic", opts =>
            {
                opts.TopicType = TopicType.Namespace;
                opts.TopicEndpoint = "http://namespace.io";
                opts.TopicName = "orders";
                opts.TopicAccessKey = "keyNs";
            });

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IEventPropagationClientFactory>();

        // When
        var customClient = factory.CreateClient("CustomTopic");
        var namespaceClient = factory.CreateClient("NamespaceTopic");

        // Then
        Assert.NotNull(customClient);
        Assert.NotNull(namespaceClient);
        Assert.NotSame(customClient, namespaceClient);
    }
}