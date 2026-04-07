using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.ServiceBus;

/// <summary>
/// Builds gRPC binding data for <c>serviceBusTrigger</c> functions.
/// Reads the raw message body bytes from <c>context.InputData["$messageBodyBytes"]</c> and
/// optional trigger metadata JSON from <c>context.InputData["$triggerMetadata"]</c>, mapping
/// them to the trigger parameter binding and trigger metadata.
/// </summary>
public sealed class ServiceBusTriggerBinding : ITriggerBinding
{
    /// <inheritdoc/>
    public string TriggerType => "serviceBusTrigger";

    /// <inheritdoc/>
    public TriggerBindingData CreateBindingData(FunctionInvocationContext context, FunctionRegistration function)
    {
        var bodyBytes = context.InputData.TryGetValue("$messageBodyBytes", out var b) && b is byte[] bytes
            ? bytes
            : Array.Empty<byte>();

        var triggerMetadata = context.InputData.TryGetValue("$triggerMetadata", out var m)
            ? m?.ToString()
            : null;

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithBytes(function.ParameterName, bodyBytes)],
            TriggerMetadataJson = string.IsNullOrEmpty(triggerMetadata)
                ? null
                : new Dictionary<string, string>(StringComparer.Ordinal)
                    { [function.ParameterName] = triggerMetadata }
        };
    }
}
