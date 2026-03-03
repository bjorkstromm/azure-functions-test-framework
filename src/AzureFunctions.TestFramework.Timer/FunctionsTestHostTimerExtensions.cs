using System.Text.Json;
using System.Text.Json.Serialization;
using AzureFunctions.TestFramework.Core;
using Microsoft.Azure.Functions.Worker;

namespace AzureFunctions.TestFramework.Timer;

/// <summary>
/// Extension methods for invoking timer-triggered Azure Functions via <see cref="IFunctionsTestHost"/>.
/// </summary>
public static class FunctionsTestHostTimerExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Invokes a timer-triggered function by name.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the timer function (case-insensitive).</param>
    /// <param name="timerInfo">
    /// Optional timer info to pass to the function.  When <see langword="null"/>, a default
    /// <see cref="TimerInfo"/> with <c>IsPastDue = false</c> and no schedule status is used.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeTimerAsync(
        this IFunctionsTestHost host,
        string functionName,
        TimerInfo? timerInfo = null,
        CancellationToken cancellationToken = default)
    {
        var info = timerInfo ?? new TimerInfo();
        var json = JsonSerializer.Serialize(info, _jsonOptions);

        var context = new FunctionInvocationContext
        {
            TriggerType = "timerTrigger",
            InputData = { ["$timerJson"] = json }
        };

        return host.Invoker.InvokeAsync(functionName, context, cancellationToken);
    }
}
