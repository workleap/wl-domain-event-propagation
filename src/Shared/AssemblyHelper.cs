using System.Reflection;

namespace Workleap.DomainEventPropagation;

internal static class AssemblyHelper
{
    public static IEnumerable<Type> GetConcreteHandlerTypes(Assembly assembly)
    {
        if (assembly == null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }

        return assembly.GetTypes().Where(IsConcreteDomainEventHandlerType);
    }

    public static bool IsIDomainEventHandler(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>);
    }

    private static bool IsConcreteDomainEventHandlerType(Type type)
    {
        return !type.IsAbstract && type.GetInterfaces().Any(IsIDomainEventHandler);
    }
}