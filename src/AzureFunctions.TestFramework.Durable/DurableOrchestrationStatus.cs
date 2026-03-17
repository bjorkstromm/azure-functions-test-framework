using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureFunctions.TestFramework.Durable;

/// <summary>
/// Represents a Durable Functions orchestration status document returned over HTTP.
/// </summary>
public sealed class DurableOrchestrationStatus
{
    /// <summary>
    /// Gets or sets the orchestration name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the instance ID.
    /// </summary>
    [JsonPropertyName("instanceId")]
    public string? InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the runtime status.
    /// </summary>
    [JsonPropertyName("runtimeStatus")]
    public string? RuntimeStatus { get; set; }

    /// <summary>
    /// Gets or sets the serialized orchestration input.
    /// </summary>
    [JsonPropertyName("input")]
    public JsonElement Input { get; set; }

    /// <summary>
    /// Gets or sets the serialized orchestration output.
    /// </summary>
    [JsonPropertyName("output")]
    public JsonElement Output { get; set; }

    /// <summary>
    /// Gets or sets the serialized custom status.
    /// </summary>
    [JsonPropertyName("customStatus")]
    public JsonElement CustomStatus { get; set; }

    /// <summary>
    /// Gets or sets the created timestamp.
    /// </summary>
    [JsonPropertyName("createdTime")]
    public DateTimeOffset? CreatedTime { get; set; }

    /// <summary>
    /// Gets or sets the last updated timestamp.
    /// </summary>
    [JsonPropertyName("lastUpdatedTime")]
    public DateTimeOffset? LastUpdatedTime { get; set; }

    /// <summary>
    /// Gets a value indicating whether the orchestration is in a terminal state.
    /// </summary>
    [JsonIgnore]
    public bool IsTerminal =>
        RuntimeStatus is "Completed" or "Failed" or "Terminated" or "Canceled";

    /// <summary>
    /// Reads the output as a plain string when possible.
    /// </summary>
    /// <returns>The output string, or <see langword="null"/> when no output exists.</returns>
    public string? ReadOutputAsString()
    {
        return Output.ValueKind switch
        {
            JsonValueKind.String => Output.GetString(),
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => Output.GetRawText()
        };
    }
}
