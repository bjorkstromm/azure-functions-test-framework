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
    private const string QueuePayloadKey = "$queuePayload";

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

    private abstract record QueuePayload
    {
        public sealed record Message(QueueMessage Value) : QueuePayload;
        public sealed record Bytes(byte[] Value) : QueuePayload;
        public sealed record Json(string Value) : QueuePayload;
    }

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
            InputData = { [QueuePayloadKey] = new QueuePayload.Message(message) }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingData, cancellationToken);
    }

    /// <summary>
    /// Invokes a queue-triggered function by name with the specified string message.
    /// Use this overload when the function parameter is typed as <c>string</c>.
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
            InputData = { [QueuePayloadKey] = new QueuePayload.Bytes(body) }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingData, cancellationToken);
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
            InputData = { [QueuePayloadKey] = new QueuePayload.Bytes(message) }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingData, cancellationToken);
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
        var json = SerializePayloadToJson(payload, typeof(T), options);

        var context = new FunctionInvocationContext
        {
            TriggerType = "queueTrigger",
            InputData = { [QueuePayloadKey] = new QueuePayload.Json(json) }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingData, cancellationToken);
    }

    private static TriggerBindingData CreateBindingData(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var payload = context.InputData.TryGetValue(QueuePayloadKey, out var value) && value is QueuePayload queuePayload
            ? queuePayload
            : throw new InvalidOperationException("Queue payload not found in invocation context.");

        var bindingData = payload switch
        {
            QueuePayload.Message(var message) => FunctionBindingData.WithModelBindingData(
                function.ParameterName,
                ToModelBindingDataValue(message)),
            QueuePayload.Bytes(var messageBytes) => FunctionBindingData.WithBytes(function.ParameterName, messageBytes),
            QueuePayload.Json(var json) => FunctionBindingData.WithJson(function.ParameterName, json),
            _ => throw new InvalidOperationException($"Unsupported queue payload type '{payload.GetType().FullName}'.")
        };

        return new TriggerBindingData
        {
            InputData = [bindingData]
        };
    }

    private static string SerializePayloadToJson(object payload, Type payloadType, JsonSerializerOptions options)
    {
        try
        {
            return JsonSerializer.Serialize(payload, payloadType, options);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to serialize queue trigger payload of type '{payloadType.FullName}'.", ex);
        }
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
