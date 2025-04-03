using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Workleap.DomainEventPropagation;

internal static class TracingHelper
{
    // https://github.com/open-telemetry/opentelemetry-specification/blob/v1.18.0/specification/common/mapping-to-non-otlp.md#span-status
    internal const string StatusCodeTag = "otel.status_code";
    internal const string StatusDescriptionTag = "otel.status_description";

    // https://github.com/open-telemetry/opentelemetry-specification/blob/v1.18.0/specification/logs/semantic_conventions/exceptions.md
    internal const string ExceptionTypeTag = "exception.type";
    internal const string ExceptionMessageTag = "exception.message";
    internal const string ExceptionStackTraceTag = "exception.stacktrace";

    // https://github.com/open-telemetry/opentelemetry-specification/blob/v1.18.0/specification/trace/semantic_conventions/cloudevents.md#conventions
    internal const string EventGridEventsPublisherActivityType = "EventGridEvents create";
    internal const string EventGridEventsSubscriberActivityType = "EventGridEvents process";

    // https://github.com/open-telemetry/opentelemetry-specification/blob/v1.18.0/specification/trace/semantic_conventions/cloudevents.md#conventions
    internal const string CloudEventsPublisherActivityType = "CloudEvents create";
    internal const string CloudEventsSubscriberActivityType = "CloudEvents process";

    // https://github.com/open-telemetry/semantic-conventions/blob/0d80f21f77447edc97b12350ebe8bf2b2c5582ae/docs/cloudevents/cloudevents-spans.md
    internal const string CloudEventsIdTag = "cloudevents.event_id";
    internal const string CloudEventsSourceTag = "cloudevents.event_source";
    internal const string CloudEventsTypeTag = "cloudevents.event_type";

    internal const string EventgridEventsIdTag = "eventgridevents.event_id";
    internal const string EventgridEventsSourceTag = "eventgridevents.event_source";
    internal const string EventgridEventsTypeTag = "eventgridevents.event_type";

    private static readonly Assembly Assembly = typeof(TracingHelper).Assembly;
    private static readonly AssemblyName AssemblyName = Assembly.GetName();
    private static readonly string AssemblyVersion = Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? AssemblyName.Version!.ToString();

    private static readonly ActivitySource ActivitySource = new(nameof(Workleap) + "." + nameof(DomainEventPropagation), AssemblyVersion);

    public static Activity? StartProducerActivity(string activityName)
    {
        return ActivitySource.StartActivity(activityName, ActivityKind.Producer);
    }

    public static Activity? StartConsumerActivity(string activityName, ActivityContext linkedActivityContext)
    {
        var activityLinks = linkedActivityContext == default ? Enumerable.Empty<ActivityLink>() : [new ActivityLink(linkedActivityContext)];
        return ActivitySource.StartActivity(activityName, ActivityKind.Consumer, default(ActivityContext), links: activityLinks);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MarkAsSuccessful(Activity activity)
    {
        activity.AddTag(StatusCodeTag, "OK");
        activity.SetStatus(ActivityStatusCode.Ok);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MarkAsFailed(Activity activity, Exception ex)
    {
        activity.AddTag(StatusCodeTag, "ERROR");
        activity.SetStatus(ActivityStatusCode.Error);

        if (activity.IsAllDataRequested)
        {
            activity.AddTag(StatusDescriptionTag, ex.Message);
            activity.AddTag(ExceptionTypeTag, ex.GetType().FullName);
            activity.AddTag(ExceptionMessageTag, ex.Message);
            activity.AddTag(ExceptionStackTraceTag, ex.StackTrace);
        }
    }

    public static bool IsEventGridActivity(Activity activity) => ActivitySource.Name == activity.Source.Name;

    internal static string GetEventGridEventsPublisherActivityName(string eventType) => $"{EventGridEventsPublisherActivityType} {eventType}";

    internal static string GetEventGridEventsSubscriberActivityName(string eventType) => $"{EventGridEventsSubscriberActivityType} {eventType}";

    internal static string GetCloudEventsPublisherActivityName(string eventType) => $"{CloudEventsPublisherActivityType} {eventType}";

    internal static string GetCloudEventsSubscriberActivityName(string eventType) => $"{CloudEventsSubscriberActivityType} {eventType}";
}