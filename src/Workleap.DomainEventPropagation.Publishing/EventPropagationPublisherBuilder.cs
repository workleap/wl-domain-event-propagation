using Azure;
using Azure.Core;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.Namespaces;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Workleap.DomainEventPropagation;

internal sealed class EventPropagationPublisherBuilder : IEventPropagationPublisherBuilder
{
    public EventPropagationPublisherBuilder(IServiceCollection services)
    {
        this.Services = services;
    }

    public EventPropagationPublisherBuilder(IServiceCollection services, Action<EventPropagationPublisherOptions> configure)
    {
        this.Services = services;
        this.AddRegistrations(configure);
    }

    public IServiceCollection Services { get; }

    public IEventPropagationPublisherBuilder AddTopic(string topicName)
        => this.AddTopic(topicName, _ => { });

    public IEventPropagationPublisherBuilder AddTopic(string topicName, Action<EventPropagationPublisherOptions> configureOptions)
    {
        if (topicName == null)
        {
            throw new ArgumentNullException(nameof(topicName));
        }

        if (configureOptions == null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }

        this.Services.AddOptions<EventPropagationPublisherOptions>(topicName)
            .Configure<IConfiguration>((opt, cfg) => cfg.GetSection($"{EventPropagationPublisherOptions.SectionName}:{topicName}").Bind(opt))
            .Configure(configureOptions);

        this.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<EventPropagationPublisherOptions>, EventPropagationPublisherOptionsValidator>());
        this.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPublishingDomainEventBehavior, TracingPublishingDomainEventBehavior>());
        this.Services.TryAddSingleton<IEventPropagationClientFactory, EventPropagationClientFactory>();

        this.Services.AddAzureClients(builder =>
        {
            builder.AddClient<EventGridPublisherClient, EventGridPublisherClientOptions>((opts, sp) => NamedEventGridPublisherClientFactory(opts, sp, topicName))
                .WithName(topicName)
                .ConfigureOptions(ConfigureClientOptions);

            builder.AddClient<EventGridSenderClient, EventGridSenderClientOptions>((opts, sp) => NamedEventGridClientFactory(opts, sp, topicName))
                .WithName(topicName)
                .ConfigureOptions(ConfigureClientOptions);
        });

        return this;
    }

    private void AddRegistrations(Action<EventPropagationPublisherOptions> configure)
    {
        this.Services
            .AddOptions<EventPropagationPublisherOptions>()
            .Configure<IConfiguration>(BindFromWellKnownConfigurationSection)
            .Configure(configure);

        this.Services.TryAddSingleton<IEventPropagationClient, EventPropagationClient>();
        this.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<EventPropagationPublisherOptions>, EventPropagationPublisherOptionsValidator>());
        this.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPublishingDomainEventBehavior, TracingPublishingDomainEventBehavior>());

        this.Services.AddAzureClients(ConfigureEventPublisherClients);
    }

    private static void BindFromWellKnownConfigurationSection(EventPropagationPublisherOptions options, IConfiguration configuration)
    {
        configuration.GetSection(EventPropagationPublisherOptions.SectionName).Bind(options);
    }

    private static void ConfigureEventPublisherClients(AzureClientFactoryBuilder builder)
    {
        builder.AddClient<EventGridPublisherClient, EventGridPublisherClientOptions>(EventGridPublisherClientFactory)
            .WithName(EventPropagationPublisherOptions.EventGridClientName)
            .ConfigureOptions(ConfigureClientOptions);

        builder.AddClient<EventGridSenderClient, EventGridSenderClientOptions>(EventGridClientFactory)
            .WithName(EventPropagationPublisherOptions.EventGridClientName)
            .ConfigureOptions(ConfigureClientOptions);
    }

    private static EventGridPublisherClient EventGridPublisherClientFactory(EventGridPublisherClientOptions clientOptions, IServiceProvider serviceProvider)
    {
        var publisherOptions = serviceProvider.GetRequiredService<IOptions<EventPropagationPublisherOptions>>().Value;
        var topicEndpointUri = new Uri(publisherOptions.TopicEndpoint);

        return publisherOptions.TokenCredential is not null
            ? new EventGridPublisherClient(topicEndpointUri, publisherOptions.TokenCredential, clientOptions)
            : new EventGridPublisherClient(topicEndpointUri, new AzureKeyCredential(publisherOptions.TopicAccessKey), clientOptions);
    }

    private static EventGridSenderClient EventGridClientFactory(EventGridSenderClientOptions clientOptions, IServiceProvider serviceProvider)
    {
        var publisherOptions = serviceProvider.GetRequiredService<IOptions<EventPropagationPublisherOptions>>().Value;
        var topicEndpointUri = new Uri(publisherOptions.TopicEndpoint);

        return publisherOptions.TokenCredential is not null
            ? new EventGridSenderClient(topicEndpointUri, publisherOptions.TopicName, publisherOptions.TokenCredential, clientOptions)
            : new EventGridSenderClient(topicEndpointUri, publisherOptions.TopicName, new AzureKeyCredential(publisherOptions.TopicAccessKey), clientOptions);
    }

    private static EventGridPublisherClient NamedEventGridPublisherClientFactory(EventGridPublisherClientOptions clientOptions, IServiceProvider serviceProvider, string topicName)
    {
        var publisherOptions = serviceProvider.GetRequiredService<IOptionsMonitor<EventPropagationPublisherOptions>>().Get(topicName);
        var topicEndpointUri = new Uri(publisherOptions.TopicEndpoint);

        return publisherOptions.TokenCredential is not null
            ? new EventGridPublisherClient(topicEndpointUri, publisherOptions.TokenCredential, clientOptions)
            : new EventGridPublisherClient(topicEndpointUri, new AzureKeyCredential(publisherOptions.TopicAccessKey), clientOptions);
    }

    private static EventGridSenderClient NamedEventGridClientFactory(EventGridSenderClientOptions clientOptions, IServiceProvider serviceProvider, string topicName)
    {
        var publisherOptions = serviceProvider.GetRequiredService<IOptionsMonitor<EventPropagationPublisherOptions>>().Get(topicName);
        var topicEndpointUri = new Uri(publisherOptions.TopicEndpoint);

        return publisherOptions.TokenCredential is not null
            ? new EventGridSenderClient(topicEndpointUri, publisherOptions.TopicName, publisherOptions.TokenCredential, clientOptions)
            : new EventGridSenderClient(topicEndpointUri, publisherOptions.TopicName, new AzureKeyCredential(publisherOptions.TopicAccessKey), clientOptions);
    }

    private static void ConfigureClientOptions(ClientOptions options)
    {
        options.Retry.Mode = RetryMode.Fixed;
        options.Retry.MaxRetries = 1;
        options.Retry.NetworkTimeout = TimeSpan.FromSeconds(4);
    }
}