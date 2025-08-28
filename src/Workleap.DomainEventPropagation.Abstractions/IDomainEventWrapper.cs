namespace Workleap.DomainEventPropagation;

public interface IDomainEventWrapper
{
    string? Id { get; }

    string? Source { get; }

    string DomainEventName { get; }

    EventSchema DomainEventSchema { get; }

    void SetData(string key, string value);

    bool TryGetData(string key, out string? value);

    void SetMetadata(string key, string value);

    bool TryGetMetadata(string key, out string? value);
}
