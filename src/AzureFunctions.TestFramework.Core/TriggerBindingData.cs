namespace AzureFunctions.TestFramework.Core;

/// <summary>
/// Contains the binding data produced by a trigger binding factory for a single
/// non-HTTP function invocation: the input parameters and optional trigger metadata.
/// </summary>
public sealed class TriggerBindingData
{
    /// <summary>
    /// Gets the input parameter bindings to include in the gRPC <c>InvocationRequest.InputData</c>.
    /// </summary>
    public required IReadOnlyList<FunctionBindingData> InputData { get; init; }

    /// <summary>
    /// Gets optional trigger metadata entries to include in the gRPC
    /// <c>InvocationRequest.TriggerMetadata</c>, keyed by metadata name with JSON string values.
    /// </summary>
    public IReadOnlyDictionary<string, string>? TriggerMetadataJson { get; init; }
}
