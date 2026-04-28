using System.Text;
using System.Text.Json;
using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Dapr;

/// <summary>
/// Extension methods for invoking Dapr-triggered Azure Functions via <see cref="IFunctionsTestHost"/>.
/// </summary>
public static class FunctionsTestHostDaprExtensions
{
    private static readonly JsonSerializerOptions _defaultJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // -------------------------------------------------------------------------
    // DaprBindingTrigger
    // -------------------------------------------------------------------------

    /// <summary>
    /// Invokes a Dapr input binding–triggered function by name with the specified string payload.
    /// Use this overload when the function parameter is typed as <c>string</c>.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Dapr binding trigger function (case-insensitive).</param>
    /// <param name="data">The binding data passed as UTF-8 bytes to the trigger binding.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeDaprBindingAsync(
        this IFunctionsTestHost host,
        string functionName,
        string data,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(functionName);
        ArgumentNullException.ThrowIfNull(data);

        var bytes = Encoding.UTF8.GetBytes(data);

        var context = new FunctionInvocationContext
        {
            TriggerType = "daprBindingTrigger",
            InputData = { ["$daprBindingBytes"] = bytes }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingDataFromBytes, cancellationToken);
    }

    /// <summary>
    /// Invokes a Dapr input binding–triggered function by name with the specified JSON-serialized payload.
    /// Use this overload when the function parameter is a serializable reference type deserialized from JSON.
    /// </summary>
    /// <typeparam name="T">The payload type serialized to JSON.</typeparam>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Dapr binding trigger function (case-insensitive).</param>
    /// <param name="data">The object serialized as JSON for the trigger binding.</param>
    /// <param name="jsonSerializerOptions">Optional JSON serializer options; defaults to camel-case property names.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeDaprBindingAsync<T>(
        this IFunctionsTestHost host,
        string functionName,
        T data,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(functionName);
        ArgumentNullException.ThrowIfNull(data);

        var options = jsonSerializerOptions ?? _defaultJsonOptions;
        var json = JsonSerializer.Serialize(data, typeof(T), options);

        var context = new FunctionInvocationContext
        {
            TriggerType = "daprBindingTrigger",
            InputData = { ["$daprBindingJson"] = json }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingDataFromJson, cancellationToken);
    }

    // -------------------------------------------------------------------------
    // DaprServiceInvocationTrigger
    // -------------------------------------------------------------------------

    /// <summary>
    /// Invokes a Dapr service invocation–triggered function by name with no body.
    /// Use this overload for functions that do not require a request body.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Dapr service invocation function (case-insensitive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeDaprServiceInvocationAsync(
        this IFunctionsTestHost host,
        string functionName,
        CancellationToken cancellationToken = default)
        => InvokeDaprServiceInvocationAsync(host, functionName, body: string.Empty, cancellationToken);

    /// <summary>
    /// Invokes a Dapr service invocation–triggered function by name with the specified string body.
    /// Use this overload when the function parameter is typed as <c>string</c>.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Dapr service invocation function (case-insensitive).</param>
    /// <param name="body">The invocation body passed as UTF-8 bytes to the trigger binding.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeDaprServiceInvocationAsync(
        this IFunctionsTestHost host,
        string functionName,
        string body,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(functionName);
        ArgumentNullException.ThrowIfNull(body);

        var bytes = Encoding.UTF8.GetBytes(body);

        var context = new FunctionInvocationContext
        {
            TriggerType = "daprServiceInvocationTrigger",
            InputData = { ["$daprInvocationBytes"] = bytes }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateInvocationBindingDataFromBytes, cancellationToken);
    }

    /// <summary>
    /// Invokes a Dapr service invocation–triggered function by name with the specified JSON-serialized body.
    /// Use this overload when the function parameter is a serializable reference type deserialized from JSON.
    /// </summary>
    /// <typeparam name="T">The body type serialized to JSON.</typeparam>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Dapr service invocation function (case-insensitive).</param>
    /// <param name="body">The object serialized as JSON for the trigger binding.</param>
    /// <param name="jsonSerializerOptions">Optional JSON serializer options; defaults to camel-case property names.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeDaprServiceInvocationAsync<T>(
        this IFunctionsTestHost host,
        string functionName,
        T body,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(functionName);
        ArgumentNullException.ThrowIfNull(body);

        var options = jsonSerializerOptions ?? _defaultJsonOptions;
        var json = JsonSerializer.Serialize(body, typeof(T), options);

        var context = new FunctionInvocationContext
        {
            TriggerType = "daprServiceInvocationTrigger",
            InputData = { ["$daprInvocationJson"] = json }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateInvocationBindingDataFromJson, cancellationToken);
    }

    // -------------------------------------------------------------------------
    // DaprTopicTrigger
    // -------------------------------------------------------------------------

    /// <summary>
    /// Invokes a Dapr pub/sub topic–triggered function by name with the specified string message.
    /// Use this overload when the function parameter is typed as <c>string</c>.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Dapr topic trigger function (case-insensitive).</param>
    /// <param name="message">The message data passed as UTF-8 bytes to the trigger binding.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeDaprTopicAsync(
        this IFunctionsTestHost host,
        string functionName,
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(functionName);
        ArgumentNullException.ThrowIfNull(message);

        var bytes = Encoding.UTF8.GetBytes(message);

        var context = new FunctionInvocationContext
        {
            TriggerType = "daprTopicTrigger",
            InputData = { ["$daprTopicBytes"] = bytes }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateTopicBindingDataFromBytes, cancellationToken);
    }

    /// <summary>
    /// Invokes a Dapr pub/sub topic–triggered function by name with the specified JSON-serialized message.
    /// Use this overload when the function parameter is a serializable reference type deserialized from JSON.
    /// </summary>
    /// <typeparam name="T">The message type serialized to JSON.</typeparam>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Dapr topic trigger function (case-insensitive).</param>
    /// <param name="message">The object serialized as JSON for the trigger binding.</param>
    /// <param name="jsonSerializerOptions">Optional JSON serializer options; defaults to camel-case property names.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeDaprTopicAsync<T>(
        this IFunctionsTestHost host,
        string functionName,
        T message,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(functionName);
        ArgumentNullException.ThrowIfNull(message);

        var options = jsonSerializerOptions ?? _defaultJsonOptions;
        var json = JsonSerializer.Serialize(message, typeof(T), options);

        var context = new FunctionInvocationContext
        {
            TriggerType = "daprTopicTrigger",
            InputData = { ["$daprTopicJson"] = json }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateTopicBindingDataFromJson, cancellationToken);
    }

    // -------------------------------------------------------------------------
    // Binding data factories
    // -------------------------------------------------------------------------

    private static string GetJsonFromContext(FunctionInvocationContext context, string key)
        => context.InputData.TryGetValue(key, out var j) ? j?.ToString() ?? "{}" : "{}";

    private static TriggerBindingData CreateBindingDataFromBytes(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var bytes = context.InputData.TryGetValue("$daprBindingBytes", out var b) && b is byte[] bs
            ? bs
            : Array.Empty<byte>();

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithBytes(function.ParameterName, bytes)]
        };
    }

    private static TriggerBindingData CreateBindingDataFromJson(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var json = GetJsonFromContext(context, "$daprBindingJson");
        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithJson(function.ParameterName, json)]
        };
    }

    private static TriggerBindingData CreateInvocationBindingDataFromBytes(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var bytes = context.InputData.TryGetValue("$daprInvocationBytes", out var b) && b is byte[] bs
            ? bs
            : Array.Empty<byte>();

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithBytes(function.ParameterName, bytes)]
        };
    }

    internal static TriggerBindingData CreateInvocationBindingDataFromJson(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var json = GetJsonFromContext(context, "$daprInvocationJson");
        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithJson(function.ParameterName, json)]
        };
    }

    private static TriggerBindingData CreateTopicBindingDataFromBytes(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var bytes = context.InputData.TryGetValue("$daprTopicBytes", out var b) && b is byte[] bs
            ? bs
            : Array.Empty<byte>();

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithBytes(function.ParameterName, bytes)]
        };
    }

    internal static TriggerBindingData CreateTopicBindingDataFromJson(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var json = GetJsonFromContext(context, "$daprTopicJson");
        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithJson(function.ParameterName, json)]
        };
    }
}
