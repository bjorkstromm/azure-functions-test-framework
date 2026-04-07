using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.EventGrid;

/// <summary>
/// Builds gRPC binding data for <c>eventGridTrigger</c> functions.
/// Reads the JSON-serialised event from <c>context.InputData["$eventJson"]</c> and maps
/// it to the trigger parameter binding.
/// </summary>
public sealed class EventGridTriggerBinding : ITriggerBinding
{
    /// <inheritdoc/>
    public string TriggerType => "eventGridTrigger";

    /// <inheritdoc/>
    public TriggerBindingData CreateBindingData(FunctionInvocationContext context, FunctionRegistration function)
    {
        var json = context.InputData.TryGetValue("$eventJson", out var j) ? j?.ToString() ?? "{}" : "{}";

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithJson(function.ParameterName, json)]
        };
    }
}
