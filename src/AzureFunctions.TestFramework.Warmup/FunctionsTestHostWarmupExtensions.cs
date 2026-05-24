using System.Text.Json;
using System.Text.Json.Serialization;
using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Warmup;

/// <summary>
/// Extension methods for invoking warmup-triggered Azure Functions via <see cref="IFunctionsTestHost"/>.
/// </summary>
public static class FunctionsTestHostWarmupExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Invokes a warmup-triggered function by name.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the warmup function (case-insensitive).</param>
    /// <param name="context">Optional warmup context to pass to the function.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeWarmupAsync(
        this IFunctionsTestHost host,
        string functionName,
        WarmupContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var json = context is null ? "{}" : JsonSerializer.Serialize(context, _jsonOptions);

        var invocationContext = new FunctionInvocationContext
        {
            TriggerType = "warmupTrigger",
            InputData = { ["$warmupContextJson"] = json }
        };

        return host.Invoker.InvokeAsync(functionName, invocationContext, CreateBindingData, cancellationToken);
    }

    private static TriggerBindingData CreateBindingData(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var json = context.InputData.TryGetValue("$warmupContextJson", out var j) ? j?.ToString() ?? "{}" : "{}";

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithJson(function.ParameterName, json)]
        };
    }
}
