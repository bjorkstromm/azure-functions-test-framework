namespace AzureFunctions.TestFramework.Core;

/// <summary>
/// Contains the result of a function invocation.
/// </summary>
public class FunctionInvocationResult
{
    /// <summary>
    /// Gets or sets the invocation ID.
    /// </summary>
    public string InvocationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the invocation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the output data from the function.
    /// </summary>
    public Dictionary<string, object> OutputData { get; set; } = new();

    /// <summary>
    /// Gets or sets the return value from the function.
    /// </summary>
    public object? ReturnValue { get; set; }

    /// <summary>
    /// Gets or sets the error information if the invocation failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the logs generated during the invocation.
    /// </summary>
    public List<string> Logs { get; set; } = new();
}
