using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Workleap.DomainEventPropagation;

internal static class EventsApi
{
    internal static async Task<IResult> HandleEvents(
        HttpRequest request,
        [FromServices] IEventGridRequestHandler eventGridRequestHandler,
        CancellationToken cancellationToken)
    {
        var events = await BinaryData.FromStreamAsync(request.Body, cancellationToken).ConfigureAwait(false);

        EventGridRequestResult? result;

        try
        {
            if (TryParseMany(events, out EventGridEvent[] eventGridEvents))
            {
                result = await eventGridRequestHandler.HandleRequestAsync(eventGridEvents, cancellationToken).ConfigureAwait(false);
            }
            else if (TryParseMany(events, out CloudEvent[] cloudEvents))
            {
                result = await eventGridRequestHandler.HandleRequestAsync(cloudEvents, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new NotSupportedException("Unknown payload, only EventGridEvent or CloudEvent are supported");
            }

            return result?.RequestType switch
            {
                EventGridRequestType.Subscription => Results.Ok(result.ValidationResponse),
                _ => Results.Ok(),
            };
        }
        catch (ThrottlingException)
        {
            return Results.StatusCode(429);
        }
    }

    private static bool TryParseMany(BinaryData binaryEvents, out EventGridEvent[] events)
    {
        try
        {
            events = EventGridEvent.ParseMany(binaryEvents);
            return true;
        }
        catch (Exception)
        {
            events = [];
            return false;
        }
    }

    private static bool TryParseMany(BinaryData binaryEvents, out CloudEvent[] events)
    {
        try
        {
            events = CloudEvent.ParseMany(binaryEvents);
            return true;
        }
        catch (Exception)
        {
            events = [];
            return false;
        }
    }

    internal static class Routes
    {
        internal const string DomainEvents = "eventgrid/domainevents";
    }
}