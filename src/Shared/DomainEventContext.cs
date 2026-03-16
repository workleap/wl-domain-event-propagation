namespace Workleap.DomainEventPropagation;

internal sealed class DomainEventContext : IDomainEventContext
{
    private static readonly AsyncLocal<DomainEventContext?> CurrentContext = new();

    internal static DomainEventContext? Current
    {
        get => CurrentContext.Value;
        set => CurrentContext.Value = value;
    }

    public string? Id { get; init; }

    public string? Source { get; init; }

    public required string DomainEventName { get; init; }

    public EventSchema EventSchema { get; init; }
}