using Microsoft.Extensions.DependencyInjection;

namespace Workleap.DomainEventPropagation;

public interface IEventPropagationPublisherBuilder
{
    IServiceCollection Services { get; }

    IEventPropagationPublisherBuilder AddBehavior<TBehavior>() where TBehavior : class, IPublishingDomainEventBehavior;

    IEventPropagationPublisherBuilder AddBehavior(IPublishingDomainEventBehavior behavior);
}