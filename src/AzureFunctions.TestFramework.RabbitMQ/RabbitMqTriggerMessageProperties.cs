using System.Text.Json;

namespace AzureFunctions.TestFramework.RabbitMQ;

/// <summary>
/// Optional RabbitMQ delivery and application properties supplied alongside the message body
/// for synthetic RabbitMQ trigger invocations. Values are written to gRPC
/// <c>InvocationRequest.TriggerMetadata</c> using the same key names as the in-process
/// WebJobs RabbitMQ trigger binding contract (see <c>CreateBindingDataContract</c> in
/// <c>Microsoft.Azure.WebJobs.Extensions.RabbitMQ</c>).
/// </summary>
public sealed class RabbitMqTriggerMessageProperties
{
    private static readonly JsonSerializerOptions _serializeOptions = new()
    {
        PropertyNamingPolicy = null
    };

    /// <summary>
    /// Gets or sets the AMQP exchange the message was published to.
    /// </summary>
    public string? Exchange { get; set; }

    /// <summary>
    /// Gets or sets the routing key used when the message was published.
    /// </summary>
    public string? RoutingKey { get; set; }

    /// <summary>
    /// Gets or sets the consumer tag for the delivering consumer.
    /// </summary>
    public string? ConsumerTag { get; set; }

    /// <summary>
    /// Gets or sets the server-assigned delivery tag.
    /// </summary>
    public ulong? DeliveryTag { get; set; }

    /// <summary>
    /// Gets or sets whether this message has been redelivered.
    /// </summary>
    public bool? Redelivered { get; set; }

    /// <summary>
    /// Gets or sets the application message identifier (AMQP basic property).
    /// </summary>
    public string? MessageId { get; set; }

    /// <summary>
    /// Gets or sets the content type (AMQP basic property).
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier (AMQP basic property).
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the reply-to address (AMQP basic property).
    /// </summary>
    public string? ReplyTo { get; set; }

    /// <summary>
    /// Gets or sets optional application headers (serialized as a JSON object in trigger metadata).
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Returns <see langword="true"/> if any property has been set.
    /// </summary>
    public bool HasAnySet() =>
        Exchange is not null
        || RoutingKey is not null
        || ConsumerTag is not null
        || DeliveryTag.HasValue
        || Redelivered.HasValue
        || MessageId is not null
        || ContentType is not null
        || CorrelationId is not null
        || ReplyTo is not null
        || (Headers is not null && Headers.Count > 0);

    /// <summary>
    /// Builds trigger metadata entries for gRPC invocation. Each value is a JSON document string
    /// suitable for <c>TypedData.Json</c>, matching how other trigger extensions populate metadata.
    /// </summary>
    internal IReadOnlyDictionary<string, string>? ToTriggerMetadataJson()
    {
        if (!HasAnySet())
        {
            return null;
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (Exchange is not null)
        {
            map["Exchange"] = JsonSerializer.Serialize(Exchange, _serializeOptions);
        }

        if (RoutingKey is not null)
        {
            map["RoutingKey"] = JsonSerializer.Serialize(RoutingKey, _serializeOptions);
        }

        if (ConsumerTag is not null)
        {
            map["ConsumerTag"] = JsonSerializer.Serialize(ConsumerTag, _serializeOptions);
        }

        if (DeliveryTag.HasValue)
        {
            map["DeliveryTag"] = JsonSerializer.Serialize(DeliveryTag.Value, _serializeOptions);
        }

        if (Redelivered.HasValue)
        {
            map["Redelivered"] = JsonSerializer.Serialize(Redelivered.Value, _serializeOptions);
        }

        if (MessageId is not null)
        {
            map["MessageId"] = JsonSerializer.Serialize(MessageId, _serializeOptions);
        }

        if (ContentType is not null)
        {
            map["ContentType"] = JsonSerializer.Serialize(ContentType, _serializeOptions);
        }

        if (CorrelationId is not null)
        {
            map["CorrelationId"] = JsonSerializer.Serialize(CorrelationId, _serializeOptions);
        }

        if (ReplyTo is not null)
        {
            map["ReplyTo"] = JsonSerializer.Serialize(ReplyTo, _serializeOptions);
        }

        if (Headers is not null && Headers.Count > 0)
        {
            map["Headers"] = JsonSerializer.Serialize(Headers, _serializeOptions);
        }

        return map;
    }
}
