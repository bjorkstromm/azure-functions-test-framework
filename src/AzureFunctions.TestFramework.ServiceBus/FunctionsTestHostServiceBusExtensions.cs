using System.Text;
using System.Text.Json;
using AzureFunctions.TestFramework.Core;
using Azure.Messaging.ServiceBus;

namespace AzureFunctions.TestFramework.ServiceBus;

/// <summary>
/// Extension methods for invoking Service Bus–triggered Azure Functions via <see cref="IFunctionsTestHost"/>.
/// </summary>
public static class FunctionsTestHostServiceBusExtensions
{
    /// <summary>
    /// The binding source identifier used by the Azure Functions Service Bus extension
    /// to identify AMQP-encoded message binding data.
    /// </summary>
    private const string ServiceBusBindingSource = "AzureServiceBusReceivedMessage";

    /// <summary>
    /// The MIME content type used for AMQP-encoded Service Bus message content in ModelBindingData.
    /// </summary>
    private const string ServiceBusBinaryContentType = "application/octet-stream";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Invokes a Service Bus–triggered function with UTF-8 message text.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The function name (case-insensitive).</param>
    /// <param name="message">The message text, passed without JSON quoting.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeServiceBusAsync(
        this IFunctionsTestHost host,
        string functionName,
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return InvokeServiceBusAsync(host, functionName, Encoding.UTF8.GetBytes(message), cancellationToken);
    }

    /// <summary>
    /// Invokes a Service Bus–triggered function with raw message bytes, without text encoding.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The function name (case-insensitive).</param>
    /// <param name="message">The raw message body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeServiceBusAsync(
        this IFunctionsTestHost host,
        string functionName,
        byte[] message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(functionName);
        ArgumentNullException.ThrowIfNull(message);

        var context = new FunctionInvocationContext
        {
            TriggerType = "serviceBusTrigger",
            InputData = { ["$messageBodyBytes"] = message }
        };
        return host.Invoker.InvokeAsync(functionName, context, CreateBindingDataFromBytes, cancellationToken);
    }

    /// <summary>
    /// Invokes a Service Bus–triggered function with a <see cref="BinaryData"/> body, preserving its bytes.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The function name (case-insensitive).</param>
    /// <param name="message">The message body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeServiceBusAsync(
        this IFunctionsTestHost host,
        string functionName,
        BinaryData message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return InvokeServiceBusAsync(host, functionName, message.ToArray(), cancellationToken);
    }

    /// <summary>
    /// Invokes a Service Bus–triggered function with a JSON payload using camel-case property names.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The function name (case-insensitive).</param>
    /// <param name="payload">The payload to serialize as JSON.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeServiceBusAsync<T>(
        this IFunctionsTestHost host,
        string functionName,
        T payload,
        CancellationToken cancellationToken = default)
        => InvokeServiceBusAsync(host, functionName, payload, jsonSerializerOptions: null, cancellationToken);

    /// <summary>
    /// Invokes a Service Bus–triggered function with a JSON-serialized payload.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The function name (case-insensitive).</param>
    /// <param name="payload">The payload to serialize as JSON.</param>
    /// <param name="jsonSerializerOptions">Optional serializer options; defaults to camel-case property names.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeServiceBusAsync<T>(
        this IFunctionsTestHost host,
        string functionName,
        T payload,
        JsonSerializerOptions? jsonSerializerOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(functionName);
        ArgumentNullException.ThrowIfNull(payload);

        var context = new FunctionInvocationContext
        {
            TriggerType = "serviceBusTrigger",
            InputData = { ["$messageBodyJson"] = JsonSerializer.Serialize(payload, jsonSerializerOptions ?? _jsonOptions) }
        };
        return host.Invoker.InvokeAsync(functionName, context, CreateBindingDataFromJson, cancellationToken);
    }

    /// <summary>
    /// Invokes a Service Bus–triggered function by name using a <see cref="ServiceBusMessage"/>.
    /// The message body is passed as raw bytes; use this overload when the function parameter
    /// is typed as <c>string</c> or <c>byte[]</c>.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Service Bus function (case-insensitive).</param>
    /// <param name="message">
    /// The <see cref="ServiceBusMessage"/> to simulate as the incoming trigger message.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeServiceBusAsync(
        this IFunctionsTestHost host,
        string functionName,
        ServiceBusMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var bodyBytes = message.Body?.ToArray() ?? Array.Empty<byte>();

        // Build trigger metadata so the worker can bind ServiceBusReceivedMessage parameters.
        var metadata = new
        {
            MessageId = message.MessageId,
            CorrelationId = message.CorrelationId,
            ContentType = message.ContentType,
            Subject = message.Subject,
            SessionId = message.SessionId,
            ReplyTo = message.ReplyTo,
            ReplyToSessionId = message.ReplyToSessionId,
            To = message.To,
            PartitionKey = message.PartitionKey,
            TimeToLiveTotalSeconds = message.TimeToLive.TotalSeconds,
            ScheduledEnqueueTime = message.ScheduledEnqueueTime,
        };
        var triggerMetadataJson = JsonSerializer.Serialize(metadata, _jsonOptions);

        var context = new FunctionInvocationContext
        {
            TriggerType = "serviceBusTrigger",
            InputData =
            {
                ["$messageBodyBytes"] = bodyBytes,
                ["$triggerMetadata"] = triggerMetadataJson
            }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingDataFromBytes, cancellationToken);
    }

    /// <summary>
    /// Invokes a Service Bus–triggered function by name using a <see cref="ServiceBusReceivedMessage"/>.
    /// The message is AMQP-encoded and passed as <c>ModelBindingData</c>; use this overload when
    /// the function parameter is typed as <c>ServiceBusReceivedMessage</c>.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Service Bus function (case-insensitive).</param>
    /// <param name="message">
    /// The <see cref="ServiceBusReceivedMessage"/> to simulate as the incoming trigger message.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeServiceBusAsync(
        this IFunctionsTestHost host,
        string functionName,
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var context = new FunctionInvocationContext
        {
            TriggerType = "serviceBusTrigger",
            InputData =
            {
                ["$receivedMessages"] = new[] { message }
            }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingDataFromReceivedMessages, cancellationToken);
    }

    /// <summary>
    /// Invokes a Service Bus batch–triggered function by name.
    /// Use this overload when the function has <c>IsBatched = true</c> and the parameter is typed
    /// as <c>ServiceBusReceivedMessage[]</c> or <c>IReadOnlyList&lt;ServiceBusReceivedMessage&gt;</c>.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Service Bus batch function (case-insensitive).</param>
    /// <param name="messages">
    /// The collection of <see cref="ServiceBusReceivedMessage"/> instances to deliver as a batch.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeServiceBusBatchAsync(
        this IFunctionsTestHost host,
        string functionName,
        IReadOnlyList<ServiceBusReceivedMessage> messages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (messages.Count == 0)
            throw new ArgumentException("Batch must contain at least one message.", nameof(messages));
        if (messages.Any(message => message is null))
            throw new ArgumentException("Batch must not contain null messages.", nameof(messages));

        var context = new FunctionInvocationContext
        {
            TriggerType = "serviceBusTrigger",
            InputData =
            {
                ["$receivedMessages"] = messages.ToArray(),
                ["$isBatch"] = true
            }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingDataFromReceivedMessages, cancellationToken);
    }

    private static TriggerBindingData CreateBindingDataFromJson(
        FunctionInvocationContext context,
        FunctionRegistration function)
        => new()
        {
            InputData = [FunctionBindingData.WithJson(function.ParameterName, (string)context.InputData["$messageBodyJson"]!)]
        };

    private static TriggerBindingData CreateBindingDataFromBytes(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var bodyBytes = context.InputData.TryGetValue("$messageBodyBytes", out var b) && b is byte[] bytes
            ? bytes
            : Array.Empty<byte>();

        var triggerMetadata = context.InputData.TryGetValue("$triggerMetadata", out var m)
            ? m?.ToString()
            : null;

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithBytes(function.ParameterName, bodyBytes)],
            TriggerMetadataJson = string.IsNullOrEmpty(triggerMetadata)
                ? null
                : new Dictionary<string, string>(StringComparer.Ordinal)
                    { [function.ParameterName] = triggerMetadata }
        };
    }

    private static TriggerBindingData CreateBindingDataFromReceivedMessages(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var messages = context.InputData.TryGetValue("$receivedMessages", out var m)
            && m is ServiceBusReceivedMessage[] msgs
            ? msgs
            : Array.Empty<ServiceBusReceivedMessage>();

        if (!context.InputData.ContainsKey("$isBatch"))
        {
            // Single message: use ModelBindingData
            var modelData = ToModelBindingDataValue(messages[0]);
            return new TriggerBindingData
            {
                InputData = [FunctionBindingData.WithModelBindingData(function.ParameterName, modelData)]
            };
        }
        else
        {
            // Batch mode: use CollectionModelBindingData
            var items = messages.Select(ToModelBindingDataValue).ToList();
            return new TriggerBindingData
            {
                InputData = [FunctionBindingData.WithCollectionModelBindingData(function.ParameterName, items)]
            };
        }
    }

    /// <summary>
    /// Encodes a <see cref="ServiceBusReceivedMessage"/> as a <see cref="ModelBindingDataValue"/>
    /// in the format expected by the Azure Functions Service Bus extension's
    /// <c>ServiceBusReceivedMessageConverter</c>: 16-byte lock token followed by
    /// AMQP-encoded message bytes.
    /// </summary>
    private static ModelBindingDataValue ToModelBindingDataValue(ServiceBusReceivedMessage message)
    {
        // Re-encode the received message via its raw AMQP representation.
        var amqpBytes = message.GetRawAmqpMessage().ToBytes().ToArray();

        // Prepend the 16-byte lock token (parse the lock token GUID, fall back to a new GUID).
        byte[] lockTokenBytes;
        if (Guid.TryParse(message.LockToken, out var lockGuid))
            lockTokenBytes = lockGuid.ToByteArray();
        else
            lockTokenBytes = Guid.NewGuid().ToByteArray();

        var content = new byte[lockTokenBytes.Length + amqpBytes.Length];
        lockTokenBytes.CopyTo(content, 0);
        amqpBytes.CopyTo(content, lockTokenBytes.Length);

        return new ModelBindingDataValue
        {
            Version = "1.0",
            Source = ServiceBusBindingSource,
            ContentType = ServiceBusBinaryContentType,
            Content = content
        };
    }
}
