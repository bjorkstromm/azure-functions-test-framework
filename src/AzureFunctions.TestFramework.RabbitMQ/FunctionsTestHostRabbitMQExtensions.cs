using System.Text;
using System.Text.Json;
using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.RabbitMQ;

/// <summary>
/// Extension methods for invoking RabbitMQ-triggered Azure Functions via <see cref="IFunctionsTestHost"/>.
/// </summary>
public static class FunctionsTestHostRabbitMQExtensions
{
    private const string RabbitMqMessageBytesKey = "$rabbitMqMessageBytes";
    private const string RabbitMqMessageJsonKey = "$rabbitMqMessageJson";
    private const string RabbitMqPropertiesKey = "$rabbitMqProperties";

    private static readonly JsonSerializerOptions _defaultJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Invokes a RabbitMQ-triggered function by name with the specified UTF-8 text payload.
    /// Use this overload when the function parameter is typed as <see cref="string"/>.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the RabbitMQ function (case-insensitive).</param>
    /// <param name="message">The message body text passed as UTF-8 bytes to the trigger binding.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeRabbitMQAsync(
        this IFunctionsTestHost host,
        string functionName,
        string message,
        CancellationToken cancellationToken = default)
        => InvokeRabbitMQAsync(host, functionName, message, messageProperties: null, cancellationToken);

    /// <summary>
    /// Invokes a RabbitMQ-triggered function by name with the specified UTF-8 text payload and optional
    /// delivery / application properties mapped to trigger metadata (for example
    /// <c>FunctionContext.BindingContext.BindingData</c> entries).
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the RabbitMQ function (case-insensitive).</param>
    /// <param name="message">The message body text passed as UTF-8 bytes to the trigger binding.</param>
    /// <param name="messageProperties">Optional RabbitMQ metadata; when <see langword="null"/>, no extra trigger metadata is sent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeRabbitMQAsync(
        this IFunctionsTestHost host,
        string functionName,
        string message,
        RabbitMqTriggerMessageProperties? messageProperties,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(functionName);
        ArgumentNullException.ThrowIfNull(message);

        var body = Encoding.UTF8.GetBytes(message);

        var context = new FunctionInvocationContext
        {
            TriggerType = "rabbitMQTrigger",
            InputData = { [RabbitMqMessageBytesKey] = body }
        };

        if (messageProperties is not null)
        {
            context.InputData[RabbitMqPropertiesKey] = messageProperties;
        }

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingDataFromBytes, cancellationToken);
    }

    /// <summary>
    /// Invokes a RabbitMQ-triggered function by name with the specified raw message body.
    /// Use this overload when the function parameter is typed as <c>byte[]</c> or <see cref="BinaryData"/>.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the RabbitMQ function (case-insensitive).</param>
    /// <param name="body">The raw message body bytes passed to the trigger binding.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeRabbitMQAsync(
        this IFunctionsTestHost host,
        string functionName,
        byte[] body,
        CancellationToken cancellationToken = default)
        => InvokeRabbitMQAsync(host, functionName, body, messageProperties: null, cancellationToken);

    /// <summary>
    /// Invokes a RabbitMQ-triggered function by name with the specified raw message body and optional
    /// delivery / application properties in trigger metadata.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the RabbitMQ function (case-insensitive).</param>
    /// <param name="body">The raw message body bytes passed to the trigger binding.</param>
    /// <param name="messageProperties">Optional RabbitMQ metadata; when <see langword="null"/>, no extra trigger metadata is sent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeRabbitMQAsync(
        this IFunctionsTestHost host,
        string functionName,
        byte[] body,
        RabbitMqTriggerMessageProperties? messageProperties,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(functionName);
        ArgumentNullException.ThrowIfNull(body);

        var context = new FunctionInvocationContext
        {
            TriggerType = "rabbitMQTrigger",
            InputData = { [RabbitMqMessageBytesKey] = body }
        };

        if (messageProperties is not null)
        {
            context.InputData[RabbitMqPropertiesKey] = messageProperties;
        }

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingDataFromBytes, cancellationToken);
    }

    /// <summary>
    /// Invokes a RabbitMQ-triggered function by name with a JSON-serialized POCO payload.
    /// Use this overload when the function parameter is a serializable reference type deserialized from JSON
    /// (isolated worker model).
    /// </summary>
    /// <typeparam name="T">The payload type serialized to JSON.</typeparam>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the RabbitMQ function (case-insensitive).</param>
    /// <param name="payload">The object serialized as JSON for the trigger binding.</param>
    /// <param name="jsonSerializerOptions">Optional JSON serializer options; defaults to camel-case property names.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeRabbitMQAsync<T>(
        this IFunctionsTestHost host,
        string functionName,
        T payload,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => InvokeRabbitMQAsync(host, functionName, payload, messageProperties: null, jsonSerializerOptions, cancellationToken);

    /// <summary>
    /// Invokes a RabbitMQ-triggered function by name with a JSON-serialized POCO payload and optional
    /// delivery / application properties in trigger metadata.
    /// </summary>
    /// <typeparam name="T">The payload type serialized to JSON.</typeparam>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the RabbitMQ function (case-insensitive).</param>
    /// <param name="payload">The object serialized as JSON for the trigger binding.</param>
    /// <param name="messageProperties">Optional RabbitMQ metadata; when <see langword="null"/>, no extra trigger metadata is sent.</param>
    /// <param name="jsonSerializerOptions">Optional JSON serializer options; defaults to camel-case property names.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeRabbitMQAsync<T>(
        this IFunctionsTestHost host,
        string functionName,
        T payload,
        RabbitMqTriggerMessageProperties? messageProperties,
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
            TriggerType = "rabbitMQTrigger",
            InputData = { [RabbitMqMessageJsonKey] = json }
        };

        if (messageProperties is not null)
        {
            context.InputData[RabbitMqPropertiesKey] = messageProperties;
        }

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingDataFromJson, cancellationToken);
    }

    private static TriggerBindingData CreateBindingDataFromBytes(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var messageBytes = context.InputData.TryGetValue(RabbitMqMessageBytesKey, out var b) && b is byte[] bytes
            ? bytes
            : Array.Empty<byte>();

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithBytes(function.ParameterName, messageBytes)],
            TriggerMetadataJson = MergeOptionalTriggerMetadata(context)
        };
    }

    private static TriggerBindingData CreateBindingDataFromJson(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var json = context.InputData.TryGetValue(RabbitMqMessageJsonKey, out var j)
            ? j?.ToString() ?? "{}"
            : "{}";

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithJson(function.ParameterName, json)],
            TriggerMetadataJson = MergeOptionalTriggerMetadata(context)
        };
    }

    private static IReadOnlyDictionary<string, string>? MergeOptionalTriggerMetadata(FunctionInvocationContext context)
    {
        if (!context.InputData.TryGetValue(RabbitMqPropertiesKey, out var o) || o is not RabbitMqTriggerMessageProperties props)
        {
            return null;
        }

        return props.ToTriggerMetadataJson();
    }
}
