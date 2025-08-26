using System.Text.Json;

namespace Workleap.DomainEventPropagation;

internal class JsonSerializerConstants
{
    public static readonly JsonSerializerOptions DomainEventSerializerOptions = new(JsonSerializerDefaults.Web);
}