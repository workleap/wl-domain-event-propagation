using System.Collections.Concurrent;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.Namespaces;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;

namespace Workleap.DomainEventPropagation;

internal sealed class EventPropagationClientFactory : IEventPropagationClientFactory
{
    private readonly IAzureClientFactory<EventGridPublisherClient> _publisherClientFactory;
    private readonly IAzureClientFactory<EventGridSenderClient> _senderClientFactory;
    private readonly IOptionsMonitor<EventPropagationPublisherOptions> _optionsMonitor;
    private readonly IEnumerable<IPublishingDomainEventBehavior> _behaviors;
    private readonly ConcurrentDictionary<string, IEventPropagationClient> _clients = new();

    public EventPropagationClientFactory(
        IAzureClientFactory<EventGridPublisherClient> publisherClientFactory,
        IAzureClientFactory<EventGridSenderClient> senderClientFactory,
        IOptionsMonitor<EventPropagationPublisherOptions> optionsMonitor,
        IEnumerable<IPublishingDomainEventBehavior> behaviors)
    {
        this._publisherClientFactory = publisherClientFactory;
        this._senderClientFactory = senderClientFactory;
        this._optionsMonitor = optionsMonitor;
        this._behaviors = behaviors;
    }

    public IEventPropagationClient CreateClient(string name)
    {
        return this._clients.GetOrAdd(name, this.CreateClientCore);
    }

    private IEventPropagationClient CreateClientCore(string name)
    {
        var options = this._optionsMonitor.Get(name);
        return new EventPropagationClient(this._publisherClientFactory, this._senderClientFactory, options, name, this._behaviors);
    }
}