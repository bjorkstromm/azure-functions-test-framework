namespace AzureFunctions.TestFramework.Warmup;

/// <summary>
/// Represents optional payload data for warmup-triggered function invocation.
/// </summary>
public sealed class WarmupContext
{
    /// <summary>
    /// Gets or sets optional key/value properties included in the warmup payload.
    /// </summary>
    public Dictionary<string, string>? Properties { get; init; }
}
