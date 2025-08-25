namespace Workleap.DomainEventPropagation.EventGridClientAdapter;

internal interface IEventGridClientWrapperFactory
{
    IEventGridClientAdapter CreateClient(string name);
}