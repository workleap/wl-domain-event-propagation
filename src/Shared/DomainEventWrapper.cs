using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Messaging;
using Azure.Messaging.EventGrid;

namespace Workleap.DomainEventPropagation;

internal sealed class DomainEventWrapper
{
    public DomainEventWrapper(EventGridEvent eventGridEvent)
    {
        this.Id = eventGridEvent.Id;
        this.Source = eventGridEvent.Topic;
        this.Data = eventGridEvent.Data.ToObjectFromJson<JsonObject>()!;
        this.DomainEventName = eventGridEvent.EventType;
        this.DomainEventSchema = EventSchema.EventGridEvent;
    }

    public DomainEventWrapper(CloudEvent cloudEvent)
    {
        this.Id = cloudEvent.Id;
        this.Source = cloudEvent.Source;
        this.Data = cloudEvent.Data!.ToObjectFromJson<JsonObject>()!;
        this.DomainEventName = cloudEvent.Type;
        this.DomainEventSchema = EventSchema.CloudEvent;
    }

    private DomainEventWrapper(JsonObject data, string domainEventName, EventSchema schema)
    {
        this.Data = data;
        this.DomainEventName = domainEventName;
        this.DomainEventSchema = schema;
    }

    public string? Id { get; }

    public string? Source { get; }

    public JsonObject Data { get; }

    public string DomainEventName { get; }

    public EventSchema DomainEventSchema { get; }

    public void SetMetadata(string key, string value)
    {
        this.Data[GetMetadataKey(key)] = value;
    }

    public bool TryGetMetadata(string key, out string? value)
    {
        if (this.Data.TryGetPropertyValue(GetMetadataKey(key), out var nodeValue) && nodeValue != null)
        {
            value = nodeValue.GetValue<string?>();
            return true;
        }

        value = null;
        return false;
    }

    private static string GetMetadataKey(string key) => "__" + key;

    public object Unwrap(Type returnType)
    {
        return this.Data.Deserialize(returnType, JsonSerializerConstants.DomainEventSerializerOptions) ?? throw new ArgumentException("The event cannot be deserialized from JSON");
    }

    public BinaryData ToBinaryData() => BinaryData.FromObjectAsJson(this.Data);

    public static DomainEventWrapper Wrap<T>(T domainEvent)
        where T : IDomainEvent
    {
        var domainEventName = DomainEventNameCache.GetName<T>();
        var domainEventSchema = DomainEventSchemaCache.GetEventSchema<T>();
        var serializedEvent = (JsonObject?)JsonSerializer.SerializeToNode(domainEvent, domainEvent.GetType(), JsonSerializerConstants.DomainEventSerializerOptions)
            ?? throw new ArgumentException("The event cannot be serialized to JSON");

        return new DomainEventWrapper(serializedEvent, domainEventName, domainEventSchema);
    }
}