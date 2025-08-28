using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        services.TryAddSingleton<ISubscriptionEventGridWebhookHandler, SubscriptionEventGridWebhookHandler>();
        services.TryAddSingleton<IDomainEventGridWebhookHandler, DomainEventGridWebhookHandler>();
        services.TryAddSingleton<IEventGridRequestHandler, EventGridRequestHandler>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISubscriptionDomainEventBehavior, TracingSubscriptionDomainEventBehavior>());

        services.TryAddSingleton<IDomainEventTypeRegistry>(this._domainEventTypeRegistry);
    }

    public IServiceCollection Services { get; }

    public IEventPropagationSubscriberBuilder AddDomainEventHandlers(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        this.Services.AddDomainEventHandlers(this._domainEventTypeRegistry, AssemblyHelper.GetConcreteHandlerTypes(assembly));
        return this;
    }

    public IEventPropagationSubscriberBuilder AddDomainEventHandler<TEvent, THandler>()
        where THandler : IDomainEventHandler<TEvent>
        where TEvent : IDomainEvent
    {
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
}