using Azure.Core.Amqp;
using Azure.Messaging.EventHubs;
using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.EventHubs;

/// <summary>
/// Extension methods for invoking Event Hubs–triggered Azure Functions via <see cref="IFunctionsTestHost"/>.
/// </summary>
public static class FunctionsTestHostEventHubsExtensions
{
    /// <summary>
    /// The binding source identifier used by the Azure Functions Event Hubs extension
    /// to identify AMQP-encoded event data binding data.
    /// </summary>
    private const string EventHubsBindingSource = "AzureEventHubsEventData";

    /// <summary>
    /// The MIME content type used for AMQP-encoded Event Hubs event data in ModelBindingData.
    /// </summary>
    private const string EventHubsBinaryContentType = "application/octet-stream";

    /// <summary>
    /// Invokes an Event Hubs–triggered function by name with the specified <see cref="EventData"/>.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Event Hubs function (case-insensitive).</param>
    /// <param name="eventData">The event data to simulate as the trigger input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    /// <remarks>
    /// Use this overload when the function parameter is typed as <c>EventData</c> (i.e.
    /// <c>[EventHubTrigger(..., IsBatched = false)]</c>).
    /// </remarks>
    public static Task<FunctionInvocationResult> InvokeEventHubAsync(
        this IFunctionsTestHost host,
        string functionName,
        EventData eventData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        var context = new FunctionInvocationContext
        {
            TriggerType = "eventHubTrigger",
            InputData = { ["$eventData"] = new[] { eventData } }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingData, cancellationToken);
    }

    /// <summary>
    /// Invokes an Event Hubs batch–triggered function by name with the specified collection of <see cref="EventData"/>.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Event Hubs batch function (case-insensitive).</param>
    /// <param name="events">
    /// The collection of <see cref="EventData"/> instances to deliver as a batch.
    /// Must contain at least one event.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    /// <remarks>
    /// Use this overload when the function parameter is typed as <c>EventData[]</c> (i.e.
    /// <c>[EventHubTrigger(...)]</c> with the default <c>IsBatched = true</c>).
    /// </remarks>
    public static Task<FunctionInvocationResult> InvokeEventHubBatchAsync(
        this IFunctionsTestHost host,
        string functionName,
        IReadOnlyList<EventData> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0)
            throw new ArgumentException("Batch must contain at least one event.", nameof(events));

        var context = new FunctionInvocationContext
        {
            TriggerType = "eventHubTrigger",
            InputData = { ["$eventData"] = events.ToArray() }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingData, cancellationToken);
    }

    private static TriggerBindingData CreateBindingData(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var events = context.InputData.TryGetValue("$eventData", out var e) && e is EventData[] data
            ? data
            : Array.Empty<EventData>();

        if (events.Length == 1)
        {
            var modelData = ToModelBindingDataValue(events[0]);
            return new TriggerBindingData
            {
                InputData = [FunctionBindingData.WithModelBindingData(function.ParameterName, modelData)]
            };
        }
        else
        {
            var items = events.Select(ToModelBindingDataValue).ToList();
            return new TriggerBindingData
            {
                InputData = [FunctionBindingData.WithCollectionModelBindingData(function.ParameterName, items)]
            };
        }
    }

    /// <summary>
    /// Encodes an <see cref="EventData"/> as a <see cref="ModelBindingDataValue"/>
    /// in the format expected by the Azure Functions Event Hubs extension's
    /// <c>EventDataConverter</c>: AMQP-encoded event bytes with source
    /// <c>"AzureEventHubsEventData"</c>.
    /// </summary>
    private static ModelBindingDataValue ToModelBindingDataValue(EventData eventData)
    {
        var amqpBytes = eventData.GetRawAmqpMessage().ToBytes().ToArray();

        return new ModelBindingDataValue
        {
            Version = "1.0",
            Source = EventHubsBindingSource,
            ContentType = EventHubsBinaryContentType,
            Content = amqpBytes
        };
    }
}
