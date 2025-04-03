using System.Diagnostics;
using OpenTelemetry.Context.Propagation;

namespace Workleap.DomainEventPropagation;

internal sealed class TracingSubscriptionDomainEventBehavior : ISubscriptionDomainEventBehavior
{
    public async Task HandleAsync(DomainEventWrapper domainEventWrapper, DomainEventHandlerDelegate next, CancellationToken cancellationToken)
    {
        var activityName = GetSubscriptionActivityName(domainEventWrapper);
        var propagationContext = ExtractPropagationContextFromEvent(domainEventWrapper);

        using var activity = TracingHelper.StartConsumerActivity(activityName, propagationContext.ActivityContext);

        if (activity == null)
        {
            await next(domainEventWrapper, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await HandleWithTracing(domainEventWrapper, next, activity, cancellationToken).ConfigureAwait(false);
        }
    }

    private static PropagationContext ExtractPropagationContextFromEvent(DomainEventWrapper domainEventWrapper)
    {
        return Propagators.DefaultTextMapPropagator.Extract(default, domainEventWrapper, ExtractActivityProperties);
    }

    private static IEnumerable<string> ExtractActivityProperties(DomainEventWrapper domainEventWrapper, string key)
    {
        return domainEventWrapper.TryGetMetadata(key, out var value) ? [value!] : [];
    }

    private static async Task HandleWithTracing(DomainEventWrapper domainEventWrapper, DomainEventHandlerDelegate next, Activity activity, CancellationToken cancellationToken)
    {
        try
        {
            AddEventActivityTags(activity, domainEventWrapper);
            await next(domainEventWrapper, cancellationToken).ConfigureAwait(false);

            TracingHelper.MarkAsSuccessful(activity);
        }
        catch (Exception ex)
        {
            TracingHelper.MarkAsFailed(activity, ex);
            throw;
        }
    }

    private static string GetSubscriptionActivityName(DomainEventWrapper domainEventWrappers) => domainEventWrappers.DomainEventSchema switch
    {
        EventSchema.EventGridEvent => TracingHelper.GetEventGridEventsSubscriberActivityName(domainEventWrappers.DomainEventName),
        EventSchema.CloudEvent => TracingHelper.GetCloudEventsSubscriberActivityName(domainEventWrappers.DomainEventName),
        _ => TracingHelper.GetEventGridEventsSubscriberActivityName(domainEventWrappers.DomainEventName),
    };

    private static void AddEventActivityTags(Activity activity, DomainEventWrapper domainEventWrapper)
    {
        switch (domainEventWrapper.DomainEventSchema)
        {
            case EventSchema.CloudEvent:
                activity.AddTag(TracingHelper.CloudEventsIdTag, domainEventWrapper.Id);
                activity.AddTag(TracingHelper.CloudEventsSourceTag, domainEventWrapper.Source);
                activity.AddTag(TracingHelper.CloudEventsTypeTag, domainEventWrapper.DomainEventName);
                break;
            case EventSchema.EventGridEvent:
                activity.AddTag(TracingHelper.EventgridEventsIdTag, domainEventWrapper.Id);
                activity.AddTag(TracingHelper.EventgridEventsSourceTag, domainEventWrapper.Source);
                activity.AddTag(TracingHelper.EventgridEventsTypeTag, domainEventWrapper.DomainEventName);
                break;
            default:
                return;
        }
    }
}