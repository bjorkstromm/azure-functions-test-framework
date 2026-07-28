using System.Text;
using System.Text.Json;
using Azure.Storage.Queues.Models;
using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Queue;

/// <summary>
/// Extension methods for invoking queue-triggered Azure Functions via <see cref="IFunctionsTestHost"/>.
/// </summary>
public static class FunctionsTestHostQueueExtensions
{
    private const string QueueMessageKey = "$queueMessage";
    private const string QueueMessageBytesKey = "$queueMessageBytes";
    private const string QueueMessageJsonKey = "$queueMessageJson";

    /// <summary>
    /// The binding source identifier used by the Azure Functions Queue extension
    /// to identify queue message binding data.
    /// </summary>
    private const string QueueBindingSource = "AzureStorageQueues";

    /// <summary>
    /// The MIME content type used for JSON-encoded queue message content in ModelBindingData.
    /// </summary>
    private const string QueueJsonContentType = "application/json";
    private static readonly JsonSerializerOptions _defaultJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Invokes a queue-triggered function by name with the specified <see cref="QueueMessage"/>.
    /// Use this overload when the function parameter is typed as <c>QueueMessage</c>.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the queue function (case-insensitive).</param>
    /// <param name="message">The queue message to pass to the function.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeQueueAsync(
        this IFunctionsTestHost host,
        string functionName,
        QueueMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var context = new FunctionInvocationContext
        {
            TriggerType = "queueTrigger",
            InputData = { [QueueMessageKey] = message }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingDataFromQueueMessage, cancellationToken);
    }

    /// <summary>
    /// Invokes a queue-triggered function by name with the specified string message.
    /// Use this overload when the function parameter is typed as <c>string</c>,
    /// <c>byte[]</c>, or <c>BinaryData</c>.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the queue function (case-insensitive).</param>
    /// <param name="message">The message text to pass to the function.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeQueueAsync(
        this IFunctionsTestHost host,
        string functionName,
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var body = Encoding.UTF8.GetBytes(message);

        var context = new FunctionInvocationContext
        {
            TriggerType = "queueTrigger",
            InputData = { [QueueMessageBytesKey] = body }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingDataFromBytes, cancellationToken);
    }

    /// <summary>
    /// Invokes a queue-triggered function by name with the specified raw message body.
    /// Use this overload when the function parameter is typed as <c>byte[]</c> or <c>BinaryData</c>
    /// and the payload is not UTF-8 text.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the queue function (case-insensitive).</param>
    /// <param name="message">The raw message bytes to pass to the function.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeQueueAsync(
        this IFunctionsTestHost host,
        string functionName,
        byte[] message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var context = new FunctionInvocationContext
        {
            TriggerType = "queueTrigger",
            InputData = { [QueueMessageBytesKey] = message }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingDataFromBytes, cancellationToken);
    }

    /// <summary>
    /// Invokes a queue-triggered function by name with a JSON-serialized POCO payload.
    /// Use this overload when the function parameter is a serializable reference type
    /// deserialized from the queue message JSON body.
    /// </summary>
    /// <typeparam name="T">The payload type serialized to JSON.</typeparam>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the queue function (case-insensitive).</param>
    /// <param name="payload">The object serialized as JSON for the trigger binding.</param>
    /// <param name="jsonSerializerOptions">Optional JSON serializer options; defaults to camel-case property names.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeQueueAsync<T>(
        this IFunctionsTestHost host,
        string functionName,
        T payload,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var options = jsonSerializerOptions ?? _defaultJsonOptions;
        var json = JsonSerializer.Serialize(payload, typeof(T), options);

        var context = new FunctionInvocationContext
        {
            TriggerType = "queueTrigger",
            InputData = { [QueueMessageJsonKey] = json }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingDataFromJson, cancellationToken);
    }

    private static TriggerBindingData CreateBindingDataFromQueueMessage(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var message = context.InputData.TryGetValue(QueueMessageKey, out var m) && m is QueueMessage msg
            ? msg
            : throw new InvalidOperationException("QueueMessage not found in invocation context.");

        var modelData = ToModelBindingDataValue(message);
        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithModelBindingData(function.ParameterName, modelData)]
        };
    }

    private static TriggerBindingData CreateBindingDataFromBytes(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var messageBytes = context.InputData.TryGetValue(QueueMessageBytesKey, out var b) && b is byte[] bytes
            ? bytes
            : Array.Empty<byte>();

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithBytes(function.ParameterName, messageBytes)]
        };
    }

    private static TriggerBindingData CreateBindingDataFromJson(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var json = context.InputData.TryGetValue(QueueMessageJsonKey, out var j)
            ? j?.ToString() ?? "{}"
            : "{}";

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithJson(function.ParameterName, json)]
        };
    }

    /// <summary>
    /// Serializes a <see cref="QueueMessage"/> as a <see cref="ModelBindingDataValue"/>
    /// in the JSON format expected by the Azure Functions Queue extension's
    /// <c>QueueMessageConverter</c> and its internal <c>QueueMessageJsonConverter</c>.
    /// </summary>
    private static ModelBindingDataValue ToModelBindingDataValue(QueueMessage message)
    {
        var jsonBytes = SerializeQueueMessage(message);

        return new ModelBindingDataValue
        {
            Version = "1.0",
            Source = QueueBindingSource,
            ContentType = QueueJsonContentType,
            Content = jsonBytes
        };
    }

    /// <summary>
    /// Produces the JSON representation matching <c>QueueMessageJsonConverter</c>'s expected format:
    /// <c>{ "MessageId", "PopReceipt", "MessageText", "DequeueCount", "NextVisibleOn", "InsertedOn", "ExpiresOn" }</c>.
    /// </summary>
    private static byte[] SerializeQueueMessage(QueueMessage message)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteString("MessageId", message.MessageId);
        writer.WriteString("PopReceipt", message.PopReceipt);
        writer.WriteString("MessageText", message.Body?.ToString() ?? string.Empty);
        writer.WriteNumber("DequeueCount", message.DequeueCount);

        if (message.NextVisibleOn.HasValue)
            writer.WriteString("NextVisibleOn", message.NextVisibleOn.Value);
        if (message.InsertedOn.HasValue)
            writer.WriteString("InsertedOn", message.InsertedOn.Value);
        if (message.ExpiresOn.HasValue)
            writer.WriteString("ExpiresOn", message.ExpiresOn.Value);

        writer.WriteEndObject();
        writer.Flush();

        return stream.ToArray();
    }
}
