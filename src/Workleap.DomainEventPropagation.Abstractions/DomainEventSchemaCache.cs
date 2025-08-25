using System.Collections.Concurrent;
using System.Reflection;

namespace Workleap.DomainEventPropagation;

internal static class DomainEventSchemaCache
{
    // This class lives in the abstractions assembly in order to prevent having multiple instances of this same cache in different assemblies at runtime
    private static readonly ConcurrentDictionary<Type, EventSchema> DomainEventSchemaTypeMappings = new();

    public static EventSchema GetEventSchema<T>()
        where T : IDomainEvent
    {
        return GetEventSchema(typeof(T));
    }

    private static EventSchema GetEventSchema(Type domainEventType)
    {
        if (!IsConcreteDomainEvent(domainEventType))
        {
            throw new ArgumentException($"{domainEventType} must be a concrete type that implements {nameof(IDomainEvent)}");
        }

        return DomainEventSchemaTypeMappings.GetOrAdd(domainEventType, static type =>
        {
            if (type.GetCustomAttribute<DomainEventAttribute>() is { } attribute)
            {
                return attribute.EventSchema;
            }

            throw new ArgumentException($"{type} must be decorated with {nameof(DomainEventAttribute)}");
        });
    }

    private static bool IsConcreteDomainEvent(Type type) => !type.IsAbstract && typeof(IDomainEvent).IsAssignableFrom(type);
}