using Microsoft.Extensions.DependencyInjection;

namespace Workleap.DomainEventPropagation;

public interface IEventPropagationPublisherBuilder
{
    IServiceCollection Services { get; }

    IEventPropagationPublisherBuilder AddTopic(string topicName);

    IEventPropagationPublisherBuilder AddTopic(string topicName, Action<EventPropagationPublisherOptions> configureOptions);
}