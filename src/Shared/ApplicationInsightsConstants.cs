namespace Workleap.DomainEventPropagation;

internal static class ApplicationInsightsConstants
{
    public const string ProducerTelemetryKind = "Producer";
    public const string ConsumerTelemetryKind = "Consumer";
    public const string Exception = "Exception";

    // JSON metadata field containing the parent operation ID, similar to OpenTelemetry's "traceparent"
    // https://github.com/open-telemetry/opentelemetry-dotnet/blob/main-1.5.0/src/OpenTelemetry.Api/Context/Propagation/TraceContextPropagator.cs#L29
    public const string ParentOperationIdField = "opparent";

    public const string LinkedOperation = "LinkedOperation";
}