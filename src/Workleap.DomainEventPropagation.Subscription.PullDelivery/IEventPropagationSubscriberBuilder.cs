using System.Reflection;

namespace Workleap.DomainEventPropagation;

public interface IEventPropagationSubscriberBuilder
{
    IEventPropagationSubscriberBuilder AddTopicSubscription();

    IEventPropagationSubscriberBuilder AddTopicSubscription(string optionsSectionName);

    IEventPropagationSubscriberBuilder AddTopicSubscription(string optionsSectionName, Action<EventPropagationSubscriptionOptions> configureOptions);

    IEventPropagationSubscriberBuilder AddDomainEventHandlers(Assembly assembly);

    IEventPropagationSubscriberBuilder AddDomainEventHandler<TEvent, THandler>()
        where THandler : IDomainEventHandler<TEvent>
        where TEvent : IDomainEvent;

    IEventPropagationSubscriberBuilder AddBehavior<TBehavior>()
        where TBehavior : class, ISubscriptionDomainEventBehavior;

    IEventPropagationSubscriberBuilder AddBehavior(ISubscriptionDomainEventBehavior behavior);
}