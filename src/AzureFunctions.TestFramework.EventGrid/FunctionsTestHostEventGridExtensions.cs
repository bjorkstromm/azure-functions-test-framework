using System.Text.Json;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.EventGrid;

/// <summary>
/// Extension methods for invoking Event Grid–triggered Azure Functions via <see cref="IFunctionsTestHost"/>.
/// </summary>
public static class FunctionsTestHostEventGridExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Invokes an Event Grid–triggered function by name with the specified <see cref="EventGridEvent"/>.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Event Grid function (case-insensitive).</param>
    /// <param name="eventGridEvent">The Event Grid event to simulate as the trigger input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeEventGridAsync(
        this IFunctionsTestHost host,
        string functionName,
        EventGridEvent eventGridEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventGridEvent);

        var eventJson = SerializeEventGridEvent(eventGridEvent);

        var context = new FunctionInvocationContext
        {
            TriggerType = "eventGridTrigger",
            InputData = { ["$eventJson"] = eventJson }
        };

        return host.Invoker.InvokeAsync(functionName, context, cancellationToken);
    }

    /// <summary>
    /// Invokes an Event Grid–triggered function by name with the specified <see cref="CloudEvent"/>.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Event Grid function (case-insensitive).</param>
    /// <param name="cloudEvent">The Cloud Event to simulate as the trigger input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeEventGridAsync(
        this IFunctionsTestHost host,
        string functionName,
        CloudEvent cloudEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cloudEvent);

        var eventJson = SerializeCloudEvent(cloudEvent);

        var context = new FunctionInvocationContext
        {
            TriggerType = "eventGridTrigger",
            InputData = { ["$eventJson"] = eventJson }
        };

        return host.Invoker.InvokeAsync(functionName, context, cancellationToken);
    }

    /// <summary>
    /// Serializes an <see cref="EventGridEvent"/> to the EventGrid schema JSON string
    /// that the worker's <c>EventGridEventConverter</c> expects.
    /// </summary>
    private static string SerializeEventGridEvent(EventGridEvent e)
    {
        object? data = null;
        if (e.Data != null)
        {
            try { data = JsonDocument.Parse(e.Data).RootElement; }
            catch (JsonException) { data = e.Data.ToString(); }
        }

        var obj = new
        {
            id = e.Id,
            eventType = e.EventType,
            subject = e.Subject,
            eventTime = e.EventTime.UtcDateTime.ToString("o"),
            data,
            dataVersion = e.DataVersion,
            metadataVersion = "1",
            topic = string.Empty
        };

        return JsonSerializer.Serialize(obj, _jsonOptions);
    }

    /// <summary>
    /// Serializes a <see cref="CloudEvent"/> to the CloudEvents schema JSON string
    /// that the worker's <c>CloudEventConverter</c> expects.
    /// </summary>
    private static string SerializeCloudEvent(CloudEvent e)
    {
        object? data = null;
        if (e.Data != null)
        {
            try { data = JsonDocument.Parse(e.Data).RootElement; }
            catch (JsonException) { data = e.Data.ToString(); }
        }

        var obj = new
        {
            specversion = "1.0",
            id = e.Id,
            type = e.Type,
            source = e.Source,
            subject = e.Subject,
            time = e.Time?.UtcDateTime.ToString("o"),
            datacontenttype = e.DataContentType,
            data
        };

        return JsonSerializer.Serialize(obj, _jsonOptions);
    }
}
