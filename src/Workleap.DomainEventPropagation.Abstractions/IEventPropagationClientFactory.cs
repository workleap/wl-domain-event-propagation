namespace Workleap.DomainEventPropagation;

public interface IEventPropagationClientFactory
{
    IEventPropagationClient CreateClient(string name);
}