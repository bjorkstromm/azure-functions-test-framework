using System.Text;
using System.Text.Json;
using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Redis;

/// <summary>
/// Extension methods for invoking Redis-triggered Azure Functions via <see cref="IFunctionsTestHost"/>.
/// </summary>
public static class FunctionsTestHostRedisExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Invokes a Redis pub/sub–triggered function by name with the specified channel and message.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Redis pub/sub trigger function (case-insensitive).</param>
    /// <param name="channel">The Redis pub/sub channel name (informational; stored in context).</param>
    /// <param name="message">The pub/sub message payload to simulate as the trigger input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    /// <remarks>
    /// The <paramref name="message"/> is passed as a <c>string</c> binding value.
    /// Functions that declare their trigger parameter as <c>string</c> receive the raw message text.
    /// For other types the worker's Redis binding converter handles deserialization from the string.
    /// </remarks>
    public static Task<FunctionInvocationResult> InvokeRedisPubSubAsync(
        this IFunctionsTestHost host,
        string functionName,
        string channel,
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(functionName);
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(message);

        var context = new FunctionInvocationContext
        {
            TriggerType = "redisPubSubTrigger",
            InputData =
            {
                ["$channel"] = channel,
                ["$message"] = message
            }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreatePubSubBindingData, cancellationToken);
    }

    /// <summary>
    /// Invokes a Redis list–triggered function by name with the specified key and value.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Redis list trigger function (case-insensitive).</param>
    /// <param name="key">The Redis list key (informational; stored in context).</param>
    /// <param name="value">The list entry value to simulate as the trigger input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    /// <remarks>
    /// The <paramref name="value"/> is passed as a <c>string</c> binding value.
    /// Functions that declare their trigger parameter as <c>string</c> receive the raw entry text.
    /// For other types the worker's Redis binding converter handles deserialization from the string.
    /// </remarks>
    public static Task<FunctionInvocationResult> InvokeRedisListAsync(
        this IFunctionsTestHost host,
        string functionName,
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(functionName);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        var context = new FunctionInvocationContext
        {
            TriggerType = "redisListTrigger",
            InputData =
            {
                ["$key"] = key,
                ["$value"] = value
            }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateListBindingData, cancellationToken);
    }

    /// <summary>
    /// Invokes a Redis stream–triggered function by name with the specified key and stream entries.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Redis stream trigger function (case-insensitive).</param>
    /// <param name="key">The Redis stream key (informational; stored in context).</param>
    /// <param name="entries">
    /// The stream entry fields to simulate as the trigger input.
    /// Each entry is a name-value pair; the collection is serialized to a JSON array of
    /// <c>{"name":"…","value":"…"}</c> objects.
    /// Must contain at least one entry.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    /// <remarks>
    /// The entries are serialized to a JSON array and passed as the binding value.
    /// Functions that declare their trigger parameter as <c>string</c> receive the raw JSON.
    /// For other types the worker's Redis binding converter handles deserialization.
    /// </remarks>
    public static Task<FunctionInvocationResult> InvokeRedisStreamAsync(
        this IFunctionsTestHost host,
        string functionName,
        string key,
        IReadOnlyList<KeyValuePair<string, string>> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(functionName);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
            throw new ArgumentOutOfRangeException(nameof(entries), "The entries list must contain at least one entry.");

        var entriesJson = JsonSerializer.Serialize(
            entries.Select(e => new { name = e.Key, value = e.Value }).ToArray(),
            _jsonOptions);

        var context = new FunctionInvocationContext
        {
            TriggerType = "redisStreamTrigger",
            InputData =
            {
                ["$key"] = key,
                ["$entriesJson"] = entriesJson
            }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateStreamBindingData, cancellationToken);
    }

    private static TriggerBindingData CreatePubSubBindingData(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var message = context.InputData.TryGetValue("$message", out var m) ? m?.ToString() ?? string.Empty : string.Empty;

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithString(function.ParameterName, message)]
        };
    }

    private static TriggerBindingData CreateListBindingData(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var value = context.InputData.TryGetValue("$value", out var v) ? v?.ToString() ?? string.Empty : string.Empty;

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithString(function.ParameterName, value)]
        };
    }

    private static TriggerBindingData CreateStreamBindingData(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var json = context.InputData.TryGetValue("$entriesJson", out var j) ? j?.ToString() ?? "[]" : "[]";

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithString(function.ParameterName, json)]
        };
    }
}
