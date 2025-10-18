using System.Reflection;
using System.Text;
using Azure;
using Azure.Messaging.EventGrid.Namespaces;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Workleap.DomainEventPropagation.EventGridClientAdapter;

namespace Workleap.DomainEventPropagation;

internal sealed class EventPropagationSubscriberBuilder : IEventPropagationSubscriberBuilder
{
    private readonly DomainEventTypeRegistry _domainEventTypeRegistry;

    public EventPropagationSubscriberBuilder(IServiceCollection services)
    {
        this.Services = services;

        this._domainEventTypeRegistry = services.Where(x => x.ServiceType == typeof(IDomainEventTypeRegistry))
            .Select(x => x.ImplementationInstance)
            .OfType<DomainEventTypeRegistry>()
            .FirstOrDefault() ?? new DomainEventTypeRegistry();
        this.Services.TryAddSingleton<IDomainEventTypeRegistry>(this._domainEventTypeRegistry);
        this.Services.AddTransient<IEventGridClientWrapperFactory, EventGridClientAdapterFactory>();
        this.Services.AddTransient<ICloudEventHandler, CloudEventHandler>();
        this.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ISubscriptionDomainEventBehavior, TracingSubscriptionDomainEventBehavior>());
        this.Services.AddHostedService<EventPullerService>();
    }

    public IServiceCollection Services { get; }

    public IEventPropagationSubscriberBuilder AddTopicSubscription()
        => this.AddTopicSubscription(EventPropagationSubscriptionOptions.DefaultSectionName);

    public IEventPropagationSubscriberBuilder AddTopicSubscription(string optionsSectionName)
        => this.AddTopicSubscription(optionsSectionName, _ => { });

    public IEventPropagationSubscriberBuilder AddTopicSubscription(
        string optionsSectionName,
        Action<EventPropagationSubscriptionOptions> configureOptions)
    {
        if (configureOptions == null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }

        this.Services.AddOptions<EventPropagationSubscriptionOptions>(optionsSectionName)
            .Configure<IConfiguration>((opt, cfg) => BindFromWellKnownConfigurationSection(opt, cfg, optionsSectionName))
            .Configure(configureOptions);

        this.Services.AddTransient(sp => new EventGridClientDescriptor(optionsSectionName));
        this.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<EventPropagationSubscriptionOptions>, EventPropagationSubscriptionOptionsValidator>());

        this.Services.AddAzureClients(builder =>
        {
            builder.AddClient<EventGridReceiverClient, EventGridReceiverClientOptions>((opts, sp) => EventGridClientFactory(opts, sp, optionsSectionName))
                .WithName(optionsSectionName);
        });

        return this;
    }

    public IEventPropagationSubscriberBuilder AddDomainEventHandlers(Assembly assembly)
    {
        this.EnsureAtLeastOneSubscriberExists();

        var handlersToAdd = AssemblyHelper.GetConcreteHandlerTypes(assembly).ToArray();

        var handlersToAddGroupedByInterfaces = handlersToAdd
            .SelectMany(t => t
                .GetInterfaces()
                .Where(AssemblyHelper.IsIDomainEventHandler)
                .Select(i => new InterfaceImplementationPair(new InterfaceType(i), new ImplementationType(t))))
            .GroupBy(pair => pair.Interface)
            .ToArray();

        EnsureNoDuplicatesInGroups(handlersToAddGroupedByInterfaces);

        foreach (var group in handlersToAddGroupedByInterfaces)
        {
            this.EnsureHandlerInterfaceNotAlreadyRegistered(group.Key, group.Single().Implementation);
        }

        this.Services.AddDomainEventHandlers(this._domainEventTypeRegistry, handlersToAdd);
        return this;
    }

    public IEventPropagationSubscriberBuilder AddDomainEventHandler<TEvent, THandler>()
        where THandler : IDomainEventHandler<TEvent>
        where TEvent : IDomainEvent
    {
        this.EnsureAtLeastOneSubscriberExists();
        this.EnsureHandlerInterfaceNotAlreadyRegistered(new InterfaceType(typeof(IDomainEventHandler<TEvent>)), new ImplementationType(typeof(THandler)));

        this.Services.AddDomainEventHandler<TEvent, THandler>(this._domainEventTypeRegistry);

        return this;
    }

    public IEventPropagationSubscriberBuilder AddBehavior<TBehavior>()
        where TBehavior : class, ISubscriptionDomainEventBehavior
    {
        this.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ISubscriptionDomainEventBehavior, TBehavior>());
        return this;
    }

    public IEventPropagationSubscriberBuilder AddBehavior(ISubscriptionDomainEventBehavior behavior)
    {
        this.Services.TryAddEnumerable(ServiceDescriptor.Singleton(behavior));
        return this;
    }

    private static void BindFromWellKnownConfigurationSection(EventPropagationSubscriptionOptions options, IConfiguration configuration, string optionsSectionName)
    {
        var section = configuration.GetSection(optionsSectionName);
        section.Bind(options);
    }

    private static EventGridReceiverClient EventGridClientFactory(EventGridReceiverClientOptions eventGridClientOptions, IServiceProvider serviceProvider, string optionsSectionName)
    {
        var subscriberOptions = serviceProvider.GetRequiredService<IOptionsMonitor<EventPropagationSubscriptionOptions>>().Get(optionsSectionName);
        var topicEndpointUri = new Uri(subscriberOptions.TopicEndpoint);

        return subscriberOptions.TokenCredential is not null
            ? new EventGridReceiverClient(topicEndpointUri, subscriberOptions.TopicName, subscriberOptions.SubscriptionName, subscriberOptions.TokenCredential, eventGridClientOptions)
            : new EventGridReceiverClient(topicEndpointUri, subscriberOptions.TopicName, subscriberOptions.SubscriptionName, new AzureKeyCredential(subscriberOptions.TopicAccessKey), eventGridClientOptions);
    }

    private void EnsureAtLeastOneSubscriberExists()
    {
        if (this.Services.All(x => x.ServiceType != typeof(EventGridClientDescriptor)))
        {
            throw new InvalidOperationException(
                $"No subscriber was found. Please call {nameof(this.AddTopicSubscription)} before calling {nameof(this.AddDomainEventHandlers)}.");
        }
    }

    private static void EnsureNoDuplicatesInGroups(IEnumerable<IGrouping<InterfaceType, InterfaceImplementationPair>> groups)
    {
        var groupsWithDuplicates = groups
            .Where(x => x.Count() > 1)
            .ToArray();

        if (groupsWithDuplicates is { Length: 0 })
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Found duplicates handlers in assembly :");
        foreach (var group in groupsWithDuplicates)
        {
            sb.AppendLine($"- {group.Key.Value} is implemented by {string.Join(", ", group.Select(x => x.Implementation.Value))}");
        }

        throw new InvalidOperationException(sb.ToString());
    }

    private void EnsureHandlerInterfaceNotAlreadyRegistered(InterfaceType interfaceType, ImplementationType implementationType)
    {
        var existingHandler = this.Services.FirstOrDefault(x => x.ServiceType == interfaceType.Value);
        if (existingHandler != null)
        {
            throw new InvalidOperationException($"Cannot register {implementationType.Value} because {existingHandler.ImplementationType} is already registered for {interfaceType.Value}");
        }
    }

    private record InterfaceType(Type Value);

    private record ImplementationType(Type Value);

    private record InterfaceImplementationPair(InterfaceType Interface, ImplementationType Implementation);
}