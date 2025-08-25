namespace Workleap.DomainEventPropagation;

internal sealed class DomainEventHandlerNotRegisteredException : Exception
{
    public DomainEventHandlerNotRegisteredException(string domainEventName)
        : base($"A handler for the domain event {domainEventName} was not registered")
    {
    }
}