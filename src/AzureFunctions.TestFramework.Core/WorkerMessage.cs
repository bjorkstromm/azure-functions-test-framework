namespace AzureFunctions.TestFramework.Core;

/// <summary>
/// Represents a message sent to or received from the Functions worker.
/// This will be mapped to gRPC messages in the protocol implementation.
/// </summary>
public class WorkerMessage
{
    /// <summary>
    /// Gets or sets the message type.
    /// </summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request ID for correlating requests and responses.
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// Gets or sets the message payload.
    /// </summary>
    public object? Payload { get; set; }

    /// <summary>
    /// Gets or sets additional message properties.
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();
}
