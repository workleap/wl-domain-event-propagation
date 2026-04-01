using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace Workleap.DomainEventPropagation;

internal sealed class TracingPublishingDomainEventBehavior : IPublishingDomainEventBehavior
{
    public async Task HandleAsync(IDomainEventWrapperCollection domainEventWrappers, PublishingDomainEventHandlerDelegate next, CancellationToken cancellationToken)
    {
        var activityName = GetPublishingActivityName(domainEventWrappers);

        using var activity = TracingHelper.StartProducerActivity(activityName);

        if (activity == null)
        {
            await next(domainEventWrappers, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await HandleWithTracing(domainEventWrappers, next, activity, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task HandleWithTracing(IDomainEventWrapperCollection domainEventWrappers, PublishingDomainEventHandlerDelegate next, Activity activity, CancellationToken cancellationToken)
    {
        try
        {
            InjectCurrentActivityContextDataIntoEvents(domainEventWrappers);
            InjectEventMetadataIntoCurrentActivity(domainEventWrappers);

            await next(domainEventWrappers, cancellationToken).ConfigureAwait(false);

            TracingHelper.MarkAsSuccessful(activity);
        }
        catch (Exception ex)
        {
            TracingHelper.MarkAsFailed(activity, ex);
            throw;
        }
    }

    private static void InjectEventMetadataIntoCurrentActivity(IDomainEventWrapperCollection domainEventWrappers)
    {
        var activity = Activity.Current ?? default;
        if (activity == default)
        {
            return;
        }

        activity.AddTag(TracingHelper.DomainEventsTypeTag, domainEventWrappers.DomainEventName);
        activity.AddTag(TracingHelper.DomainEventsSchemaTag, domainEventWrappers.DomainSchema);
        activity.AddTag(TracingHelper.DomainEventsCountTag, domainEventWrappers.Count);
    }

    private static void InjectCurrentActivityContextDataIntoEvents(IDomainEventWrapperCollection domainEventWrappers)
    {
        var activityContextData = GetCurrentActivityContextData();

        foreach (var kvp in activityContextData)
        {
            foreach (var domainEventWrapper in domainEventWrappers)
            {
                domainEventWrapper.SetMetadata(kvp.Key, kvp.Value);
            }
        }
    }

    private static Dictionary<string, string> GetCurrentActivityContextData()
    {
        var activityContextData = new Dictionary<string, string>();

        var activityContext = Activity.Current?.Context ?? default;
        if (activityContext == default)
        {
            return activityContextData;
        }

        var baggageIgnoredForPayloadSizeAndSecurityConsiderations = default(Baggage);
        var propagationContext = new PropagationContext(activityContext, baggageIgnoredForPayloadSizeAndSecurityConsiderations);

        // See: https://www.honeycomb.io/blog/understanding-distributed-tracing-message-bus#opentelemetry_propagation_apis
        // and: https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/Instrumentation.Hangfire-1.5.0-beta.1/src/OpenTelemetry.Instrumentation.Hangfire/Implementation/HangfireInstrumentationJobFilterAttribute.cs#L113
        Propagators.DefaultTextMapPropagator.Inject(propagationContext, activityContextData, InjectActivityProperties);

        return activityContextData;
    }

    private static void InjectActivityProperties(Dictionary<string, string> activityProperties, string key, string value)
    {
        activityProperties[key] = value;
    }

    private static string GetPublishingActivityName(IDomainEventWrapperCollection domainEventWrappers) => domainEventWrappers.DomainSchema switch
    {
        EventSchema.EventGridEvent => TracingHelper.GetEventGridEventsPublisherActivityName(domainEventWrappers.DomainEventName),
        EventSchema.CloudEvent => TracingHelper.GetCloudEventsPublisherActivityName(domainEventWrappers.DomainEventName),
        _ => TracingHelper.GetEventGridEventsPublisherActivityName(domainEventWrappers.DomainEventName),
    };
}