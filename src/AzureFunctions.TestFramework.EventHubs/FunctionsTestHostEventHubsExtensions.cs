using Azure.Core.Amqp;
using Azure.Messaging.EventHubs;
using AzureFunctions.TestFramework.Core;
using System.Text.Json;

namespace AzureFunctions.TestFramework.EventHubs;

/// <summary>
/// Extension methods for invoking Event Hubs–triggered Azure Functions via <see cref="IFunctionsTestHost"/>.
/// </summary>
public static class FunctionsTestHostEventHubsExtensions
{
    private static readonly JsonSerializerOptions DefaultSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>Invokes a single-event trigger with unmodified text, not JSON-encoded text.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The function name.</param>
    /// <param name="message">The event body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeEventHubAsync(
        this IFunctionsTestHost host, string functionName, string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return InvokeRawAsync(host, functionName, name => FunctionBindingData.WithString(name, message), cancellationToken);
    }

    /// <summary>Invokes a single-event trigger with raw bytes, preserving binary content.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The function name.</param>
    /// <param name="message">The event body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeEventHubAsync(
        this IFunctionsTestHost host, string functionName, byte[] message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return InvokeRawAsync(host, functionName, name => FunctionBindingData.WithBytes(name, message), cancellationToken);
    }

    /// <summary>Invokes a single-event trigger with raw binary data.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The function name.</param>
    /// <param name="message">The event body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeEventHubAsync(
        this IFunctionsTestHost host, string functionName, BinaryData message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return host.InvokeEventHubAsync(functionName, message.ToArray(), cancellationToken);
    }

    /// <summary>Invokes a single-event trigger with a JSON-serialized payload.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The function name.</param>
    /// <param name="message">The payload to serialize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    /// <remarks>Explicit generic calls serialize even strings or byte arrays as JSON.</remarks>
    public static Task<FunctionInvocationResult> InvokeEventHubAsync<T>(
        this IFunctionsTestHost host, string functionName, T message,
        CancellationToken cancellationToken = default)
        => host.InvokeEventHubAsync(functionName, message, DefaultSerializerOptions, cancellationToken);

    /// <summary>Invokes a single-event trigger with a payload serialized using the specified JSON options.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The function name.</param>
    /// <param name="message">The payload to serialize.</param>
    /// <param name="serializerOptions">JSON options; null uses camelCase property names.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeEventHubAsync<T>(
        this IFunctionsTestHost host, string functionName, T message,
        JsonSerializerOptions? serializerOptions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return InvokeRawAsync(host, functionName,
            name => FunctionBindingData.WithJson(name, JsonSerializer.Serialize(message, serializerOptions ?? DefaultSerializerOptions)),
            cancellationToken);
    }

    /// <summary>Invokes a batch trigger with raw text bodies, including a one-event batch.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The function name.</param>
    /// <param name="messages">A non-empty batch with no null elements.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeEventHubBatchAsync(
        this IFunctionsTestHost host, string functionName, IReadOnlyList<string> messages,
        CancellationToken cancellationToken = default)
    {
        ValidateBatch(messages);
        return InvokeRawAsync(host, functionName,
            name => FunctionBindingData.WithJson(name, JsonSerializer.Serialize(messages)), cancellationToken);
    }

    /// <summary>Invokes a batch trigger with raw byte bodies, preserving binary content.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The function name.</param>
    /// <param name="messages">A non-empty batch with no null elements.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeEventHubBatchAsync(
        this IFunctionsTestHost host, string functionName, IReadOnlyList<byte[]> messages,
        CancellationToken cancellationToken = default)
    {
        ValidateBatch(messages);
        return InvokeRawAsync(host, functionName,
            name => FunctionBindingData.WithJson(name, JsonSerializer.Serialize(messages)), cancellationToken);
    }

    /// <summary>Invokes a batch trigger with raw binary bodies.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The function name.</param>
    /// <param name="messages">A non-empty batch with no null elements.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeEventHubBatchAsync(
        this IFunctionsTestHost host, string functionName, IReadOnlyList<BinaryData> messages,
        CancellationToken cancellationToken = default)
    {
        ValidateBatch(messages);
        return host.InvokeEventHubBatchAsync(functionName, messages.Select(message => message.ToArray()).ToArray(), cancellationToken);
    }

    /// <summary>Invokes a batch trigger with JSON-serialized payloads.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The function name.</param>
    /// <param name="messages">A non-empty batch with no null elements.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    /// <remarks>Explicit generic calls serialize even strings or byte arrays as JSON.</remarks>
    public static Task<FunctionInvocationResult> InvokeEventHubBatchAsync<T>(
        this IFunctionsTestHost host, string functionName, IReadOnlyList<T> messages,
        CancellationToken cancellationToken = default)
        => host.InvokeEventHubBatchAsync(functionName, messages, DefaultSerializerOptions, cancellationToken);

    /// <summary>Invokes a batch trigger with payloads serialized using the specified JSON options.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The function name.</param>
    /// <param name="messages">A non-empty batch with no null elements.</param>
    /// <param name="serializerOptions">JSON options; null uses camelCase property names.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeEventHubBatchAsync<T>(
        this IFunctionsTestHost host, string functionName, IReadOnlyList<T> messages,
        JsonSerializerOptions? serializerOptions, CancellationToken cancellationToken = default)
    {
        ValidateBatch(messages);
        return InvokeRawAsync(host, functionName,
            name => FunctionBindingData.WithJson(name, JsonSerializer.Serialize(messages, serializerOptions ?? DefaultSerializerOptions)),
            cancellationToken);
    }

    private static void ValidateBatch<T>(IReadOnlyList<T> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (messages.Count == 0)
            throw new ArgumentException("Batch must contain at least one event.", nameof(messages));
        if (messages.Any(message => message is null))
            throw new ArgumentException("Batch must not contain null events.", nameof(messages));
    }

    private static Task<FunctionInvocationResult> InvokeRawAsync(
        IFunctionsTestHost host, string functionName, Func<string, FunctionBindingData> createBinding,
        CancellationToken cancellationToken)
    {
        var context = new FunctionInvocationContext { TriggerType = "eventHubTrigger" };
        return host.Invoker.InvokeAsync(functionName, context,
            (_, function) => new TriggerBindingData { InputData = [createBinding(function.ParameterName)] },
            cancellationToken);
    }

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
        ValidateBatch(events);

        var context = new FunctionInvocationContext
        {
            TriggerType = "eventHubTrigger",
            InputData = { ["$eventData"] = events.ToArray(), ["$isBatch"] = true }
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

        if (!context.InputData.ContainsKey("$isBatch"))
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
