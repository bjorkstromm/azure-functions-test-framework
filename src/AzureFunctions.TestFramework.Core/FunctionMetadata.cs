namespace AzureFunctions.TestFramework.Core;

/// <summary>
/// Contains metadata about a discovered function.
/// </summary>
public class FunctionMetadata
{
    /// <summary>
    /// Gets or sets the function name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the function ID.
    /// </summary>
    public string FunctionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the entry point (method name).
    /// </summary>
    public string EntryPoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the script file path.
    /// </summary>
    public string ScriptFile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bindings (trigger and input/output bindings).
    /// </summary>
    public List<BindingMetadata> Bindings { get; set; } = new();

    /// <summary>
    /// Gets or sets additional properties.
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();
}
