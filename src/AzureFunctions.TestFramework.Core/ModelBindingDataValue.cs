namespace AzureFunctions.TestFramework.Core;

/// <summary>
/// Describes a single <c>ModelBindingData</c> value for an Azure Function invocation parameter.
/// Used to pass structured binding payloads (e.g. Service Bus AMQP message content) through
/// the gRPC <c>TypedData.ModelBindingData</c> wire format.
/// </summary>
public sealed class ModelBindingDataValue
{
    /// <summary>Gets the binding protocol version. Defaults to <c>"1.0"</c>.</summary>
    public string Version { get; init; } = "1.0";

    /// <summary>Gets the binding source identifier (e.g. <c>"AzureServiceBusReceivedMessage"</c>).</summary>
    public required string Source { get; init; }

    /// <summary>Gets the content MIME type (e.g. <c>"application/octet-stream"</c>).</summary>
    public required string ContentType { get; init; }

    /// <summary>Gets the raw binary payload for this binding.</summary>
    public required byte[] Content { get; init; }
}
