namespace AzureFunctions.TestFramework.Core;

/// <summary>
/// Contains the context data for invoking a function, including trigger data and bindings.
/// </summary>
public class FunctionInvocationContext
{
    /// <summary>
    /// Gets or sets the unique invocation ID.
    /// </summary>
    public string InvocationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the trigger type (e.g., "httpTrigger", "timerTrigger").
    /// </summary>
    public string TriggerType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the trigger metadata as key-value pairs.
    /// </summary>
    public Dictionary<string, object> TriggerMetadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the input data for the function.
    /// </summary>
    public Dictionary<string, object> InputData { get; set; } = new();

    /// <summary>
    /// Gets or sets the trace context for distributed tracing.
    /// </summary>
    public Dictionary<string, string> TraceContext { get; set; } = new();
}
