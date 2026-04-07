using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Blob;

/// <summary>
/// Builds gRPC binding data for <c>blobTrigger</c> functions.
/// Reads the raw blob content bytes from <c>context.InputData["$blobContentBytes"]</c> and
/// optional trigger metadata JSON from <c>context.InputData["$triggerMetadata"]</c>, mapping
/// them to the trigger parameter binding and trigger metadata.
/// </summary>
public sealed class BlobTriggerBinding : ITriggerBinding
{
    /// <inheritdoc/>
    public string TriggerType => "blobTrigger";

    /// <inheritdoc/>
    public TriggerBindingData CreateBindingData(FunctionInvocationContext context, FunctionRegistration function)
    {
        var contentBytes = context.InputData.TryGetValue("$blobContentBytes", out var b) && b is byte[] bytes
            ? bytes
            : Array.Empty<byte>();

        var triggerMetadata = context.InputData.TryGetValue("$triggerMetadata", out var m)
            ? m?.ToString()
            : null;

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithBytes(function.ParameterName, contentBytes)],
            TriggerMetadataJson = string.IsNullOrEmpty(triggerMetadata)
                ? null
                : new Dictionary<string, string>(StringComparer.Ordinal)
                    { [function.ParameterName] = triggerMetadata }
        };
    }
}
