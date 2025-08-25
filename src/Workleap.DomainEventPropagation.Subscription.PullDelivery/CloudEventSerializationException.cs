using System.Threading.Tasks.Dataflow;

namespace Workleap.DomainEventPropagation;

internal sealed class CloudEventSerializationException : Exception
{
    public CloudEventSerializationException(string type, Exception innerException)
        : base($"The cloud event with Type {type} could not be wrapped into a DomainEventWrapper", innerException)
    {
    }
}