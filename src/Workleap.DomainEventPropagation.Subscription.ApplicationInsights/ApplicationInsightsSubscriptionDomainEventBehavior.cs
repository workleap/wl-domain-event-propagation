using System.Diagnostics;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Workleap.DomainEventPropagation;

internal sealed class ApplicationInsightsSubscriptionDomainEventBehavior : ISubscriptionDomainEventBehavior
{
    private readonly TelemetryClient? _telemetryClient;

    public ApplicationInsightsSubscriptionDomainEventBehavior(TelemetryClient? telemetryClient)
    {
        this._telemetryClient = telemetryClient;
    }

    public Task HandleAsync(DomainEventWrapper domainEventWrapper, DomainEventHandlerDelegate next, CancellationToken cancellationToken)
    {
        return this._telemetryClient == null ? next(domainEventWrapper, cancellationToken) : this.HandleWithTelemetry(domainEventWrapper, next, cancellationToken);
    }

    private async Task HandleWithTelemetry(DomainEventWrapper domainEventWrapper, DomainEventHandlerDelegate next, CancellationToken cancellationToken)
    {
        var operation = this._telemetryClient!.StartActivityAwareDependencyOperation(
            domainEventWrapper.DomainEventName,
            TracingHelper.EventGridEventsSubscriberActivityType);

        if (domainEventWrapper.TryGetMetadata(ApplicationInsightsConstants.ParentOperationIdField, out var parentOperationId))
        {
            operation.Telemetry.Properties.TryAdd(ApplicationInsightsConstants.LinkedOperation, parentOperationId);
        }

        AddEventTelemetryProperties(domainEventWrapper, operation);

        // Originating activity must be captured AFTER that the operation is created
        // Because ApplicationInsights SDK creates another intermediate Activity
        var originatingActivity = Activity.Current;

        try
        {
            await next(domainEventWrapper, cancellationToken).ConfigureAwait(false);

            operation.Telemetry.Success = true;
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;

            if (!operation.Telemetry.Properties.ContainsKey(ApplicationInsightsConstants.Exception))
            {
                operation.Telemetry.Properties.Add(ApplicationInsightsConstants.Exception, ex.ToString());
            }

            throw;
        }
        finally
        {
            // The dependency telemetry is sent when the operation is disposed
            if (originatingActivity == null)
            {
                operation.Dispose();
            }
            else
            {
                // Attach the telemetry to the originating activity
                originatingActivity.ExecuteAsCurrentActivity(operation, static x => x.Dispose());
            }
        }
    }

    private static void AddEventTelemetryProperties(DomainEventWrapper domainEventWrapper, IOperationHolder<DependencyTelemetry> operation)
    {
        switch (domainEventWrapper.DomainEventSchema)
        {
            case EventSchema.CloudEvent:
                operation.Telemetry.Properties.TryAdd(TracingHelper.CloudEventsIdTag, domainEventWrapper.Id);
                operation.Telemetry.Properties.TryAdd(TracingHelper.CloudEventsSourceTag, domainEventWrapper.Source);
                operation.Telemetry.Properties.TryAdd(TracingHelper.CloudEventsTypeTag, domainEventWrapper.DomainEventName);
                break;
            case EventSchema.EventGridEvent:
                operation.Telemetry.Properties.TryAdd(TracingHelper.EventgridEventsIdTag, domainEventWrapper.Id);
                operation.Telemetry.Properties.TryAdd(TracingHelper.EventgridEventsSourceTag, domainEventWrapper.Source);
                operation.Telemetry.Properties.TryAdd(TracingHelper.EventgridEventsTypeTag, domainEventWrapper.DomainEventName);
                break;
            default:
                return;
        }
    }
}