using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Timer;

/// <summary>
/// Builds gRPC binding data for <c>timerTrigger</c> functions.
/// Reads the JSON-serialised <see cref="Microsoft.Azure.Functions.Worker.TimerInfo"/> from
/// <c>context.InputData["$timerJson"]</c> and maps it to the trigger parameter binding.
/// </summary>
public sealed class TimerTriggerBinding : ITriggerBinding
{
    /// <inheritdoc/>
    public string TriggerType => "timerTrigger";

    /// <inheritdoc/>
    public TriggerBindingData CreateBindingData(FunctionInvocationContext context, FunctionRegistration function)
    {
        var json = context.InputData.TryGetValue("$timerJson", out var j) ? j?.ToString() ?? "{}" : "{}";

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithJson(function.ParameterName, json)]
        };
    }
}
