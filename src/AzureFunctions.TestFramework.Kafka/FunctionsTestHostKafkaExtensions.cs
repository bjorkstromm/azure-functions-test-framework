using System.Text;
using System.Text.Json;
using AzureFunctions.TestFramework.Core;
using Microsoft.Azure.Functions.Worker;

namespace AzureFunctions.TestFramework.Kafka;

/// <summary>
/// Extension methods for invoking Kafka-triggered Azure Functions via <see cref="IFunctionsTestHost"/>.
/// </summary>
public static class FunctionsTestHostKafkaExtensions
{
    /// <summary>
    /// The binding source identifier expected by the Azure Functions Kafka extension's
    /// <c>KafkaRecordConverter</c> when deserializing <c>KafkaRecord</c> parameters.
    /// </summary>
    private const string KafkaBindingSource = "AzureKafkaRecord";

    /// <summary>
    /// The MIME content type used for proto3-encoded Kafka records in ModelBindingData.
    /// </summary>
    private const string KafkaProtoContentType = "application/x-protobuf";

    private const string KafkaMessageBytesKey = "$kafkaMessageBytes";
    private const string KafkaMessageJsonKey = "$kafkaMessageJson";
    private const string KafkaRecordKey = "$kafkaRecord";
    private const string KafkaRecordsKey = "$kafkaRecords";

    private static readonly JsonSerializerOptions _defaultJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // -------------------------------------------------------------------------
    // string overloads (single and batch)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Invokes a Kafka-triggered function by name with the specified UTF-8 text payload.
    /// Use this overload when the function parameter is typed as <c>string</c>.
    /// The string is passed as the raw binding value; the function receives the text directly.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Kafka function (case-insensitive).</param>
    /// <param name="message">The message body text passed to the trigger binding.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeKafkaAsync(
        this IFunctionsTestHost host,
        string functionName,
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(functionName);
        ArgumentNullException.ThrowIfNull(message);

        var context = new FunctionInvocationContext
        {
            TriggerType = "kafkaTrigger",
            InputData = { [KafkaMessageJsonKey] = message }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingDataFromJson, cancellationToken);
    }

    /// <summary>
    /// Invokes a Kafka batch-triggered function by name with the specified collection of UTF-8 text payloads.
    /// Use this overload when the function parameter is typed as <c>string[]</c> and <c>IsBatched = true</c>.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Kafka batch function (case-insensitive).</param>
    /// <param name="messages">
    /// The collection of messages to deliver as a batch. Must contain at least one message.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeKafkaBatchAsync(
        this IFunctionsTestHost host,
        string functionName,
        IReadOnlyList<string> messages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(functionName);
        ArgumentNullException.ThrowIfNull(messages);
        if (messages.Count == 0)
            throw new ArgumentException("Batch must contain at least one message.", nameof(messages));

        var jsonArray = JsonSerializer.Serialize(messages, _defaultJsonOptions);

        var context = new FunctionInvocationContext
        {
            TriggerType = "kafkaTrigger",
            InputData = { [KafkaMessageJsonKey] = jsonArray }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingDataFromJson, cancellationToken);
    }

    // -------------------------------------------------------------------------
    // byte[] overloads (single and batch)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Invokes a Kafka-triggered function by name with the specified raw message body bytes.
    /// Use this overload when the function parameter is typed as <c>byte[]</c> or <see cref="BinaryData"/>.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Kafka function (case-insensitive).</param>
    /// <param name="body">The raw message body bytes passed to the trigger binding.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeKafkaAsync(
        this IFunctionsTestHost host,
        string functionName,
        byte[] body,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(functionName);
        ArgumentNullException.ThrowIfNull(body);

        var context = new FunctionInvocationContext
        {
            TriggerType = "kafkaTrigger",
            InputData = { [KafkaMessageBytesKey] = body }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingDataFromBytes, cancellationToken);
    }

    /// <summary>
    /// Invokes a Kafka batch-triggered function by name with the specified collection of raw body bytes.
    /// Use this overload when the function parameter is typed as <c>byte[][]</c> and <c>IsBatched = true</c>.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Kafka batch function (case-insensitive).</param>
    /// <param name="bodies">
    /// The collection of raw body bytes to deliver as a batch. Must contain at least one element.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeKafkaBatchAsync(
        this IFunctionsTestHost host,
        string functionName,
        IReadOnlyList<byte[]> bodies,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(functionName);
        ArgumentNullException.ThrowIfNull(bodies);
        if (bodies.Count == 0)
            throw new ArgumentException("Batch must contain at least one message.", nameof(bodies));

        // Encode each body as a base64 string in a JSON array so the worker's string[]
        // parameter binding can decode it.  This mirrors how the real Kafka host delivers
        // binary payloads to batch-mode functions.
        var encoded = bodies.Select(b => Convert.ToBase64String(b)).ToArray();
        var jsonArray = JsonSerializer.Serialize(encoded, _defaultJsonOptions);

        var context = new FunctionInvocationContext
        {
            TriggerType = "kafkaTrigger",
            InputData = { [KafkaMessageJsonKey] = jsonArray }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingDataFromJson, cancellationToken);
    }

    // -------------------------------------------------------------------------
    // KafkaRecord overloads (single and batch)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Invokes a Kafka-triggered function by name with the specified <see cref="KafkaRecord"/>.
    /// Use this overload when the function parameter is typed as <see cref="KafkaRecord"/>.
    /// The record is proto3-encoded and delivered as <c>ModelBindingData</c>
    /// (source: <c>AzureKafkaRecord</c>, content-type: <c>application/x-protobuf</c>).
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Kafka function (case-insensitive).</param>
    /// <param name="record">The Kafka record to pass to the function.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeKafkaAsync(
        this IFunctionsTestHost host,
        string functionName,
        KafkaRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(functionName);
        ArgumentNullException.ThrowIfNull(record);

        var context = new FunctionInvocationContext
        {
            TriggerType = "kafkaTrigger",
            InputData = { [KafkaRecordKey] = record }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingDataFromRecord, cancellationToken);
    }

    /// <summary>
    /// Invokes a Kafka batch-triggered function by name with the specified collection of <see cref="KafkaRecord"/> instances.
    /// Use this overload when the function parameter is typed as <c>KafkaRecord[]</c> and <c>IsBatched = true</c>.
    /// Each record is proto3-encoded and delivered as a <c>CollectionModelBindingData</c>.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Kafka batch function (case-insensitive).</param>
    /// <param name="records">
    /// The collection of <see cref="KafkaRecord"/> instances to deliver as a batch.
    /// Must contain at least one record.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeKafkaBatchAsync(
        this IFunctionsTestHost host,
        string functionName,
        IReadOnlyList<KafkaRecord> records,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(functionName);
        ArgumentNullException.ThrowIfNull(records);
        if (records.Count == 0)
            throw new ArgumentException("Batch must contain at least one record.", nameof(records));

        var context = new FunctionInvocationContext
        {
            TriggerType = "kafkaTrigger",
            InputData = { [KafkaRecordsKey] = records.ToArray() }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingDataFromRecords, cancellationToken);
    }

    // -------------------------------------------------------------------------
    // POCO / generic JSON overloads (single and batch)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Invokes a Kafka-triggered function by name with a JSON-serialized POCO payload.
    /// Use this overload when the function parameter is a type deserialized from JSON (isolated worker model).
    /// The payload is serialized to a JSON string and passed as the binding value.
    /// </summary>
    /// <typeparam name="T">The payload type serialized to JSON.</typeparam>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Kafka function (case-insensitive).</param>
    /// <param name="payload">The object serialized as JSON for the trigger binding.</param>
    /// <param name="jsonSerializerOptions">Optional JSON serializer options; defaults to camel-case property names.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeKafkaAsync<T>(
        this IFunctionsTestHost host,
        string functionName,
        T payload,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(functionName);
        ArgumentNullException.ThrowIfNull(payload);

        var options = jsonSerializerOptions ?? _defaultJsonOptions;
        var json = JsonSerializer.Serialize(payload, typeof(T), options);

        var context = new FunctionInvocationContext
        {
            TriggerType = "kafkaTrigger",
            InputData = { [KafkaMessageJsonKey] = json }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingDataFromJson, cancellationToken);
    }

    /// <summary>
    /// Invokes a Kafka batch-triggered function by name with a collection of JSON-serialized POCO payloads.
    /// Use this overload when the function parameter is typed as an array type (<c>T[]</c>) and <c>IsBatched = true</c>.
    /// </summary>
    /// <typeparam name="T">The payload element type serialized to JSON.</typeparam>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Kafka batch function (case-insensitive).</param>
    /// <param name="payloads">
    /// The collection of objects to deliver as a batch. Must contain at least one element.
    /// </param>
    /// <param name="jsonSerializerOptions">Optional JSON serializer options; defaults to camel-case property names.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeKafkaBatchAsync<T>(
        this IFunctionsTestHost host,
        string functionName,
        IReadOnlyList<T> payloads,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(functionName);
        ArgumentNullException.ThrowIfNull(payloads);
        if (payloads.Count == 0)
            throw new ArgumentException("Batch must contain at least one payload.", nameof(payloads));

        var options = jsonSerializerOptions ?? _defaultJsonOptions;
        var jsonArray = JsonSerializer.Serialize(payloads.ToArray(), typeof(T[]), options);

        var context = new FunctionInvocationContext
        {
            TriggerType = "kafkaTrigger",
            InputData = { [KafkaMessageJsonKey] = jsonArray }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingDataFromJson, cancellationToken);
    }

    // -------------------------------------------------------------------------
    // Private binding factory methods
    // -------------------------------------------------------------------------

    private static TriggerBindingData CreateBindingDataFromJson(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var json = context.InputData.TryGetValue(KafkaMessageJsonKey, out var j)
            ? j?.ToString() ?? "{}"
            : "{}";

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithJson(function.ParameterName, json)]
        };
    }

    private static TriggerBindingData CreateBindingDataFromBytes(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var bytes = context.InputData.TryGetValue(KafkaMessageBytesKey, out var b) && b is byte[] body
            ? body
            : Array.Empty<byte>();

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithBytes(function.ParameterName, bytes)]
        };
    }

    private static TriggerBindingData CreateBindingDataFromRecord(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var record = context.InputData.TryGetValue(KafkaRecordKey, out var r) && r is KafkaRecord rec
            ? rec
            : throw new InvalidOperationException("KafkaRecord not found in invocation context.");

        var modelData = ToModelBindingDataValue(record);
        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithModelBindingData(function.ParameterName, modelData)]
        };
    }

    private static TriggerBindingData CreateBindingDataFromRecords(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var records = context.InputData.TryGetValue(KafkaRecordsKey, out var r) && r is KafkaRecord[] arr
            ? arr
            : Array.Empty<KafkaRecord>();

        var items = records.Select(ToModelBindingDataValue).ToList();
        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithCollectionModelBindingData(function.ParameterName, items)]
        };
    }

    /// <summary>
    /// Encodes a <see cref="KafkaRecord"/> as a <see cref="ModelBindingDataValue"/> in the proto3
    /// format expected by the Azure Functions Kafka extension's <c>KafkaRecordConverter</c>.
    /// </summary>
    private static ModelBindingDataValue ToModelBindingDataValue(KafkaRecord record)
    {
        var protoBytes = KafkaRecordProtoWriter.Encode(record);

        return new ModelBindingDataValue
        {
            Version = "1.0",
            Source = KafkaBindingSource,
            ContentType = KafkaProtoContentType,
            Content = protoBytes
        };
    }
}
