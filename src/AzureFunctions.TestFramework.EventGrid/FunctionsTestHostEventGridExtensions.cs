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

        var data = TryParseJson(eventGridEvent.Data);

        var eventJson = JsonSerializer.Serialize(new
        {
            id = eventGridEvent.Id,
            eventType = eventGridEvent.EventType,
            subject = eventGridEvent.Subject,
            eventTime = eventGridEvent.EventTime.UtcDateTime.ToString("o"),
            data,
            dataVersion = eventGridEvent.DataVersion,
            metadataVersion = "1",
            topic = string.Empty
        }, _jsonOptions);

        host.Invoker.RegisterTriggerBinding(new EventGridTriggerBinding());

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

        var data = TryParseJson(cloudEvent.Data);

        var eventJson = JsonSerializer.Serialize(new
        {
            specversion = "1.0",
            id = cloudEvent.Id,
            type = cloudEvent.Type,
            source = cloudEvent.Source,
            subject = cloudEvent.Subject,
            time = cloudEvent.Time?.UtcDateTime.ToString("o"),
            datacontenttype = cloudEvent.DataContentType,
            data
        }, _jsonOptions);

        host.Invoker.RegisterTriggerBinding(new EventGridTriggerBinding());

        var context = new FunctionInvocationContext
        {
            TriggerType = "eventGridTrigger",
            InputData = { ["$eventJson"] = eventJson }
        };

        return host.Invoker.InvokeAsync(functionName, context, cancellationToken);
    }

    /// <summary>
    /// Attempts to parse <paramref name="binaryData"/> as a JSON document element.
    /// If <paramref name="binaryData"/> is <see langword="null"/> or not valid JSON, returns
    /// <see langword="null"/> so the containing JSON object serializes the field as a null value
    /// rather than embedding an invalid token.
    /// </summary>
    private static object? TryParseJson(BinaryData? binaryData)
    {
        if (binaryData == null) return null;
        try
        {
            return JsonDocument.Parse(binaryData).RootElement;
        }
        catch (JsonException)
        {
            // Data is not valid JSON (e.g. raw string or binary).
            // Return as a plain string so the event can still be dispatched.
            return binaryData.ToString();
        }
    }
}
