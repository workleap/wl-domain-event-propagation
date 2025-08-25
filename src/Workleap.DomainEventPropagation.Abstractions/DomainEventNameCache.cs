using System.Collections.Concurrent;
using System.Reflection;

namespace Workleap.DomainEventPropagation;

internal static class DomainEventNameCache
{
    // This class lives in the abstractions assembly in order to prevent having multiple instances of this same cache in different assemblies at runtime
    private static readonly ConcurrentDictionary<Type, string> DomainEventNameTypeMappings = new ConcurrentDictionary<Type, string>();

    public static string GetName<T>()
        where T : IDomainEvent
    {
        return GetName(typeof(T));
    }

    public static string GetName(Type domainEventType)
    {
        if (!IsConcreteDomainEventType(domainEventType))
        {
            throw new ArgumentException($"{domainEventType} must be a concrete type that implements {nameof(IDomainEvent)}");
        }

        return DomainEventNameTypeMappings.GetOrAdd(domainEventType, static type =>
        {
            if (type.GetCustomAttribute<DomainEventAttribute>() is { } attribute)
            {
                return attribute.Name;
            }

            throw new ArgumentException($"{type} must be decorated with {nameof(DomainEventAttribute)}");
        });
    }

    public static bool IsConcreteDomainEventType(Type type) => !type.IsAbstract && typeof(IDomainEvent).IsAssignableFrom(type);
}