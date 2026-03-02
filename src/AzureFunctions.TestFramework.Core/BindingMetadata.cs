namespace AzureFunctions.TestFramework.Core;

/// <summary>
/// Contains metadata about a function binding (trigger or input/output).
/// </summary>
public class BindingMetadata
{
    /// <summary>
    /// Gets or sets the binding name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the binding type (e.g., "httpTrigger", "blob").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the binding direction (in, out, inout).
    /// </summary>
    public string Direction { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional binding properties.
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();
}
