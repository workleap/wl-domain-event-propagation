using System.Collections.Concurrent;
using System.Reflection;

namespace Workleap.DomainEventPropagation;

internal abstract class BaseEventHandler
{
    private const string HandleDomainEventAsyncMethodName = "HandleDomainEventAsync";

    private readonly IServiceProvider _serviceProvider;
    private readonly IDomainEventTypeRegistry _domainEventTypeRegistry;

    protected static readonly ConcurrentDictionary<Type, MethodInfo> GenericDomainEventHandlerMethodCache = new();

    protected BaseEventHandler(IServiceProvider serviceProvider, IDomainEventTypeRegistry domainEventTypeRegistry)
    {
        this._serviceProvider = serviceProvider;
        this._domainEventTypeRegistry = domainEventTypeRegistry;
    }

    protected virtual Type? GetDomainEventType(string domainEventName)
    {
        return this._domainEventTypeRegistry.GetDomainEventType(domainEventName);
    }

    protected virtual Type? GetDomainEventHandlerType(string domainEventName)
    {
        return this._domainEventTypeRegistry.GetDomainEventHandlerType(domainEventName);
    }

    protected virtual object? ResolveDomainEventHandler(Type domainEventHandlerType)
    {
        return this._serviceProvider.GetService(domainEventHandlerType);
    }

    protected Func<Task>? BuildHandleDomainEventAsyncMethod(DomainEventWrapper domainEventWrapper, CancellationToken cancellationToken)
    {
        var domainEventType = this.GetDomainEventType(domainEventWrapper.DomainEventName);
        var domainEventHandlerType = this.GetDomainEventHandlerType(domainEventWrapper.DomainEventName);

        if (domainEventType == null || domainEventHandlerType == null)
        {
            return null;
        }

        var domainEventHandler = this.ResolveDomainEventHandler(domainEventHandlerType);
        if (domainEventHandler == null)
        {
            return null;
        }

        var domainEvent = domainEventWrapper.Unwrap(domainEventType);

        var domainEventHandlerMethod = GenericDomainEventHandlerMethodCache.GetOrAdd(domainEventHandlerType, type =>
        {
            return type.GetMethod(HandleDomainEventAsyncMethodName, BindingFlags.Public | BindingFlags.Instance) ??
                   throw new InvalidOperationException($"Public method {type.FullName}.{HandleDomainEventAsyncMethodName} not found");
        });

        return () => (Task)domainEventHandlerMethod.Invoke(domainEventHandler, new[] { domainEvent, cancellationToken })!;
    }
}