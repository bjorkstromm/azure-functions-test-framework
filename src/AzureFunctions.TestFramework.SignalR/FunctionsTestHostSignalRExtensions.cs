using System.Text.Json;
using AzureFunctions.TestFramework.Core;
using Microsoft.Azure.Functions.Worker;

namespace AzureFunctions.TestFramework.SignalR;

/// <summary>
/// Extension methods for invoking SignalR-triggered Azure Functions via <see cref="IFunctionsTestHost"/>.
/// </summary>
public static class FunctionsTestHostSignalRExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Invokes a SignalR-triggered function by name with the specified <see cref="SignalRInvocationContext"/>.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the SignalR trigger function (case-insensitive).</param>
    /// <param name="invocationContext">The SignalR invocation context to simulate as the trigger input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    /// <remarks>
    /// The <paramref name="invocationContext"/> is serialized to JSON and passed as the trigger binding data.
    /// Set <see cref="SignalRInvocationContext.ConnectionId"/> and <see cref="SignalRInvocationContext.Hub"/>
    /// to match the values declared in the <c>[SignalRTrigger]</c> attribute.
    /// </remarks>
    public static Task<FunctionInvocationResult> InvokeSignalRAsync(
        this IFunctionsTestHost host,
        string functionName,
        SignalRInvocationContext invocationContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(invocationContext);

        var contextJson = JsonSerializer.Serialize(invocationContext, _jsonOptions);

        var context = new FunctionInvocationContext
        {
            TriggerType = "signalRTrigger",
            InputData = { ["$invocationContextJson"] = contextJson }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingData, cancellationToken);
    }

    private static TriggerBindingData CreateBindingData(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var json = context.InputData.TryGetValue("$invocationContextJson", out var j)
            ? j?.ToString() ?? "{}"
            : "{}";

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithJson(function.ParameterName, json)]
        };
    }
}
