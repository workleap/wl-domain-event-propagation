namespace Workleap.DomainEventPropagation;

internal sealed class DomainEventContextBehavior : ISubscriptionDomainEventBehavior
{
    public async Task HandleAsync(DomainEventWrapper domainEventWrapper, DomainEventHandlerDelegate next, CancellationToken cancellationToken)
    {
        DomainEventContext.Current = new DomainEventContext
        {
            Id = domainEventWrapper.Id,
            Source = domainEventWrapper.Source,
            DomainEventName = domainEventWrapper.DomainEventName,
            EventSchema = domainEventWrapper.DomainEventSchema,
        };

        try
        {
            await next(domainEventWrapper, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            DomainEventContext.Current = null;
        }
    }
}