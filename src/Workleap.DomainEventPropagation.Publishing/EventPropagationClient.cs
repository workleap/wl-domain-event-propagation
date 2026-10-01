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
internal sealed class EventPropagationClient : IEventPropagationClient, IFailedDomainEventRepublisher
{
    private const int EventGridMaxEventsPerBatch = 1000;
    private const string DomainEventDefaultVersion = "1.0";

    private readonly EventPropagationPublisherOptions _eventPropagationPublisherOptions;
    private readonly DomainEventsHandlerDelegate _pipeline;
    private readonly IFailedDomainEventStore? _failedDomainEventStore;
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
        IEnumerable<IPublishingDomainEventBehavior> publishingDomainEventBehaviors,
        IEnumerable<IFailedDomainEventStore> failedDomainEventStores)
    {
        this._eventPropagationPublisherOptions = eventPropagationPublisherOptions.Value;
        this._pipeline = publishingDomainEventBehaviors.Reverse().Aggregate((DomainEventsHandlerDelegate)this.SendDomainEventsAsync, BuildPipeline);
        this._failedDomainEventStore = failedDomainEventStores.LastOrDefault();

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

    private static DomainEventsHandlerDelegate BuildPipeline(DomainEventsHandlerDelegate accumulator, IPublishingDomainEventBehavior next)
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
            var publishingException = new EventPropagationPublishingException(domainEventWrappers.DomainEventName, this._eventPropagationPublisherOptions.TopicEndpoint, ex);

            // Metadata configured through the callback lives on the CloudEvent envelope and cannot be replayed from the stored data
            if (configureDomainEventMetadata == null && await this.TryStoreFailedDomainEventsAsync(domainEventWrappers, publishingException).ConfigureAwait(false))
            {
                return;
            }

            throw publishingException;
        }
    }

    public async Task RepublishAsync(FailedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent == null)
        {
            throw new ArgumentNullException(nameof(domainEvent));
        }

        var domainEventWrapper = DomainEventWrapper.FromSerializedData(domainEvent.Data, domainEvent.DomainEventName, domainEvent.Schema);
        var domainEventWrappers = DomainEventWrapperCollection.Create(domainEventWrapper);

        try
        {
            await this._pipeline(domainEventWrappers, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new EventPropagationPublishingException(domainEvent.DomainEventName, this._eventPropagationPublisherOptions.TopicEndpoint, ex);
        }
    }

    private async Task<bool> TryStoreFailedDomainEventsAsync(DomainEventWrapperCollection domainEventWrappers, EventPropagationPublishingException publishingException)
    {
        if (this._failedDomainEventStore == null)
        {
            return false;
        }

        var failedDomainEvents = domainEventWrappers
            .Select(wrapper => new FailedDomainEvent(wrapper.DomainEventName, wrapper.DomainEventSchema, wrapper.Data.ToJsonString()))
            .ToArray();

        try
        {
            // The caller's token is often the cancelled request that caused the failure, and its changes are already committed
            return await this._failedDomainEventStore.TryStoreAsync(failedDomainEvents, publishingException, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    private Task SendDomainEventsAsync(DomainEventWrapperCollection domainEventWrappers, CancellationToken cancellationToken)
    {
        return domainEventWrappers.DomainSchema switch
        {
            EventSchema.EventGridEvent => this.SendEventGridEvents(domainEventWrappers, cancellationToken),
            EventSchema.CloudEvent => this.SendCloudEvents(domainEventWrappers, cancellationToken),
            _ => throw new NotSupportedException($"Event schema {domainEventWrappers.DomainSchema} is not supported"),
        };
    }

    private async Task SendEventGridEvents(
        DomainEventWrapperCollection domainEventWrappers,
        CancellationToken cancellationToken)
    {
        if (this._eventGridPublisherClient == null)
        {
            throw new InvalidOperationException($"Unable to send eventGrid event because the {nameof(EventGridPublisherClient)} is null");
        }

        var topicType = this._eventPropagationPublisherOptions.TopicType;
        var eventGridEvents = domainEventWrappers.Select(wrapper => new EventGridEvent(
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
        DomainEventWrapperCollection domainEventWrappers,
        CancellationToken cancellationToken)
    {
        var cloudEvents = domainEventWrappers.Select(wrapper => new CloudEvent(
            type: wrapper.DomainEventName,
            source: this._eventPropagationPublisherOptions.TopicEndpoint,
            jsonSerializableData: wrapper.Data));

        if (domainEventWrappers.ConfigureDomainEventMetadata != null)
        {
            cloudEvents = cloudEvents.Select(cloudEvent =>
            {
                domainEventWrappers.ConfigureDomainEventMetadata(new DomainEventMetadataWrapper(cloudEvent));
                return cloudEvent;
            });
        }

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