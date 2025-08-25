using Azure.Messaging.EventGrid.Namespaces;
using Microsoft.Extensions.Azure;

namespace Workleap.DomainEventPropagation.EventGridClientAdapter;

internal sealed class EventGridClientAdapterFactory : IEventGridClientWrapperFactory
{
    private readonly IAzureClientFactory<EventGridReceiverClient> _factory;

    public EventGridClientAdapterFactory(IAzureClientFactory<EventGridReceiverClient> factory)
    {
        this._factory = factory;
    }

    public IEventGridClientAdapter CreateClient(string name)
    {
        var client = this._factory.CreateClient(name);
        return new EventGridClientAdapter(client);
    }
}