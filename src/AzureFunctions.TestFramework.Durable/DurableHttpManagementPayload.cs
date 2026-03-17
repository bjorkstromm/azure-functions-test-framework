using System.Text.Json.Serialization;

namespace AzureFunctions.TestFramework.Durable;

/// <summary>
/// Represents the HTTP management payload returned by Durable Functions starter endpoints.
/// </summary>
public sealed class DurableHttpManagementPayload
{
    /// <summary>
    /// Gets or sets the orchestration instance ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the status query URL.
    /// </summary>
    [JsonPropertyName("statusQueryGetUri")]
    public string? StatusQueryGetUri { get; set; }

    /// <summary>
    /// Gets or sets the send-event URL.
    /// </summary>
    [JsonPropertyName("sendEventPostUri")]
    public string? SendEventPostUri { get; set; }

    /// <summary>
    /// Gets or sets the terminate URL.
    /// </summary>
    [JsonPropertyName("terminatePostUri")]
    public string? TerminatePostUri { get; set; }

    /// <summary>
    /// Gets or sets the purge-history URL.
    /// </summary>
    [JsonPropertyName("purgeHistoryDeleteUri")]
    public string? PurgeHistoryDeleteUri { get; set; }

    /// <summary>
    /// Gets or sets the suspend URL when supported.
    /// </summary>
    [JsonPropertyName("suspendPostUri")]
    public string? SuspendPostUri { get; set; }

    /// <summary>
    /// Gets or sets the resume URL when supported.
    /// </summary>
    [JsonPropertyName("resumePostUri")]
    public string? ResumePostUri { get; set; }
}
