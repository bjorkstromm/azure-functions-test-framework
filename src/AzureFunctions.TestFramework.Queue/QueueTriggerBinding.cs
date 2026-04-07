using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Queue;

/// <summary>
/// Builds gRPC binding data for <c>queueTrigger</c> functions.
/// Reads the raw queue message bytes from <c>context.InputData["$queueMessageBytes"]</c>
/// and maps them to the trigger parameter binding.
/// </summary>
public sealed class QueueTriggerBinding : ITriggerBinding
{
    /// <inheritdoc/>
    public string TriggerType => "queueTrigger";

    /// <inheritdoc/>
    public TriggerBindingData CreateBindingData(FunctionInvocationContext context, FunctionRegistration function)
    {
        var messageBytes = context.InputData.TryGetValue("$queueMessageBytes", out var b) && b is byte[] bytes
            ? bytes
            : Array.Empty<byte>();

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithBytes(function.ParameterName, messageBytes)]
        };
    }
}
