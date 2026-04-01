using Azure;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.Namespaces;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;

namespace Workleap.DomainEventPropagation;

/// <summary>
/// https://github.com/Azure/azure-sdk-for-net/blob/master/sdk/eventgrid/Azure.Messaging.EventGrid/README.md
/// </summary>
internal sealed class EventPropagationClient : IEventPropagationClient
{
    private const int EventGridMaxEventsPerBatch = 1000;
    private const string DomainEventDefaultVersion = "1.0";

    private readonly EventPropagationPublisherOptions _eventPropagationPublisherOptions;
    private readonly PublishingDomainEventHandlerDelegate _pipeline;
    private readonly EventGridPublisherClient? _eventGridPublisherClient;
    private readonly EventGridSenderClient? _eventGridNamespaceClient;

    /// <summary>
    /// To support Namespace topic, we need to use the following EventGridClient https://github.com/Azure/azure-sdk-for-net/blob/Azure.Messaging.EventGrid_4.17.0-beta.1/sdk/eventgrid/Azure.Messaging.EventGridV2/src/Generated/EventGridClient.cs
    /// Note that even if this client has the same name, it is different from the deprecated one found here https://learn.microsoft.com/en-us/dotnet/api/microsoft.azure.eventgrid.eventgridclient?view=azure-dotnet-legacy
    /// </summary>
    public EventPropagationClient(
        IAzureClientFactory<EventGridPublisherClient> eventGridPublisherClientFactory,
        IAzureClientFactory<EventGridSenderClient> eventGridClientFactory,
        IOptions<EventPropagationPublisherOptions> eventPropagationPublisherOptions,
        IEnumerable<IPublishingDomainEventBehavior> publishingDomainEventBehaviors)
    {
        this._eventPropagationPublisherOptions = eventPropagationPublisherOptions.Value;
        this._pipeline = publishingDomainEventBehaviors.Reverse().Aggregate((PublishingDomainEventHandlerDelegate)this.SendDomainEventsAsync, BuildPipeline);

        switch (this._eventPropagationPublisherOptions.TopicType)
        {
            case TopicType.Custom:
                this._eventGridPublisherClient = eventGridPublisherClientFactory.CreateClient(EventPropagationPublisherOptions.EventGridClientName);
                break;
            case TopicType.Namespace:
                this._eventGridNamespaceClient = eventGridClientFactory.CreateClient(EventPropagationPublisherOptions.EventGridClientName);
                break;
            default:
                throw new InvalidOperationException($"Could not create the proper event grid client for topic type {this._eventPropagationPublisherOptions.TopicType}");
        }
    }

    private static PublishingDomainEventHandlerDelegate BuildPipeline(PublishingDomainEventHandlerDelegate accumulator, IPublishingDomainEventBehavior next)
    {
        return (events, cancellationToken) => next.HandleAsync(events, accumulator, cancellationToken);
    }

    public Task PublishDomainEventAsync<T>(T domainEvent, CancellationToken cancellationToken)
        where T : IDomainEvent
        => this.InternalPublishDomainEventsAsync(new[] { domainEvent }, null, cancellationToken);

    public Task PublishDomainEventAsync<T>(T domainEvent, Action<IDomainEventMetadata> configureDomainEventMetadata, CancellationToken cancellationToken)
        where T : IDomainEvent
        => this.InternalPublishDomainEventsAsync(new[] { domainEvent }, configureDomainEventMetadata, cancellationToken);

    public Task PublishDomainEventsAsync<T>(IEnumerable<T> domainEvents, CancellationToken cancellationToken)
        where T : IDomainEvent
        => this.InternalPublishDomainEventsAsync(domainEvents, null, cancellationToken);

    public Task PublishDomainEventsAsync<T>(IEnumerable<T> domainEvents, Action<IDomainEventMetadata> configureDomainEventMetadata, CancellationToken cancellationToken)
        where T : IDomainEvent
        => this.InternalPublishDomainEventsAsync(domainEvents, configureDomainEventMetadata, cancellationToken);

    private async Task InternalPublishDomainEventsAsync<T>(IEnumerable<T> domainEvents, Action<IDomainEventMetadata>? configureDomainEventMetadata, CancellationToken cancellationToken)
        where T : IDomainEvent
    {
        if (domainEvents == null)
        {
            throw new ArgumentNullException(nameof(domainEvents));
        }

        var domainEventWrappers = DomainEventWrapperCollection.Create(domainEvents, configureDomainEventMetadata);
        if (domainEventWrappers.Count == 0)
        {
            return;
        }

        if (configureDomainEventMetadata != null && domainEventWrappers.DomainSchema != EventSchema.CloudEvent)
        {
            throw new NotSupportedException("Domain event configuration is only supported for CloudEvents");
        }

        try
        {
            await this._pipeline(domainEventWrappers, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new EventPropagationPublishingException(domainEventWrappers.DomainEventName, this._eventPropagationPublisherOptions.TopicEndpoint, ex);
        }
    }

    private Task SendDomainEventsAsync(IDomainEventWrapperCollection domainEventWrappers, CancellationToken cancellationToken)
    {
        return domainEventWrappers.DomainSchema switch
        {
            EventSchema.EventGridEvent => this.SendEventGridEvents(domainEventWrappers, cancellationToken),
            EventSchema.CloudEvent => this.SendCloudEvents(domainEventWrappers, cancellationToken),
            _ => throw new NotSupportedException($"Event schema {domainEventWrappers.DomainSchema} is not supported"),
        };
    }

    private async Task SendEventGridEvents(
        IDomainEventWrapperCollection domainEventWrappers,
        CancellationToken cancellationToken)
    {
        if (this._eventGridPublisherClient == null)
        {
            throw new InvalidOperationException($"Unable to send eventGrid event because the {nameof(EventGridPublisherClient)} is null");
        }

        var topicType = this._eventPropagationPublisherOptions.TopicType;
        var eventGridEvents = domainEventWrappers
            .Cast<DomainEventWrapper>()
            .Select(wrapper => new EventGridEvent(
                subject: wrapper.DomainEventName,
                eventType: wrapper.DomainEventName,
                dataVersion: DomainEventDefaultVersion,
                data: new BinaryData(wrapper.Data)));

        switch (topicType)
        {
            case TopicType.Custom:
                break;
            case TopicType.Namespace:
                throw new NotSupportedException("Cannot send EventGridEvents to a namespace topic");
            default:
                throw new NotSupportedException($"Topic type {topicType} is not supported");
        }

        foreach (var eventBatch in Chunk(eventGridEvents, EventGridMaxEventsPerBatch))
        {
            await this._eventGridPublisherClient.SendEventsAsync(eventBatch, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendCloudEvents(
        IDomainEventWrapperCollection domainEventWrappers,
        CancellationToken cancellationToken)
    {
        var cloudEvents = domainEventWrappers
            .Cast<DomainEventWrapper>()
            .Select(wrapper => new CloudEvent(
                type: wrapper.DomainEventName,
                source: this._eventPropagationPublisherOptions.TopicEndpoint,
                jsonSerializableData: wrapper.Data));

        cloudEvents = domainEventWrappers.ConfigureDomainEventMetadataActions
            .Aggregate(cloudEvents, (current, configureDomainEventMetadataAction) => current.Select(cloudEvent =>
            {
                configureDomainEventMetadataAction(new DomainEventMetadataWrapper(cloudEvent));
                return cloudEvent;
            }));

        foreach (var eventBatch in Chunk(cloudEvents, EventGridMaxEventsPerBatch))
        {
            if (this._eventGridPublisherClient != null)
            {
                await this._eventGridPublisherClient.SendEventsAsync(eventBatch, cancellationToken).ConfigureAwait(false);
            }
            else if (this._eventGridNamespaceClient != null)
            {
                await this._eventGridNamespaceClient.SendAsync(eventBatch, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static IEnumerable<List<T>> Chunk<T>(IEnumerable<T> source, int chunkSize)
    {
        var chunk = new List<T>(chunkSize);

        foreach (var item in source)
        {
            chunk.Add(item);
            if (chunk.Count == chunkSize)
            {
                yield return chunk;
                chunk = new List<T>(chunkSize);
            }
        }

        if (chunk.Any())
        {
            yield return chunk;
        }
    }
}