namespace Workleap.DomainEventPropagation;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DomainEventAttribute : Attribute
{
    // ReSharper disable once IntroduceOptionalParameters.Global
    public DomainEventAttribute(string name) : this(name, EventSchema.EventGridEvent)
    {
    }

    public DomainEventAttribute(string name, EventSchema eventSchema)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Domain event name cannot be empty", nameof(name));
        }

        this.Name = name;
        this.EventSchema = eventSchema;
    }

    public string Name { get; }

    public EventSchema EventSchema { get; }
}