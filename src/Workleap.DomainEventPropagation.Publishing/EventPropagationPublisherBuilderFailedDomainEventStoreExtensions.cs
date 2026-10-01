using Microsoft.Extensions.DependencyInjection;

namespace Workleap.DomainEventPropagation;

public static class EventPropagationPublisherBuilderFailedDomainEventStoreExtensions
{
    /// <summary>
    /// Hands domain events that fail to publish to <typeparamref name="TStore"/> instead of throwing,
    /// so they can be republished later with <see cref="IFailedDomainEventRepublisher"/>.
    /// Publish calls that configure CloudEvent metadata are not stored, since that metadata cannot be replayed.
    /// </summary>
    public static IEventPropagationPublisherBuilder AddFailedDomainEventStore<TStore>(this IEventPropagationPublisherBuilder builder)
        where TStore : class, IFailedDomainEventStore
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.Services.AddSingleton<IFailedDomainEventStore, TStore>();

        return builder;
    }
}