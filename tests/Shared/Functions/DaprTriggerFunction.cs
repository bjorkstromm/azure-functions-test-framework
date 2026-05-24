using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Dapr;
using Microsoft.Extensions.Logging;

namespace TestProject;

/// <summary>
/// Functions that exercise Dapr binding trigger, service invocation trigger, and topic trigger.
/// </summary>
/// <param name="logger">The function logger.</param>
/// <param name="processedItems">In-memory sink for asserted side effects.</param>
public partial class DaprTriggerFunction(ILogger<DaprTriggerFunction> logger, IProcessedItemsService processedItems)
{
    /// <summary>The Dapr binding name used by the binding trigger test functions.</summary>
    public const string BindingName = "test-dapr-binding";

    /// <summary>The Dapr pub/sub name used by the topic trigger test functions.</summary>
    public const string PubSubName = "test-pubsub";

    /// <summary>The Dapr pub/sub topic name used by the topic trigger test functions.</summary>
    public const string TopicName = "test-topic";

    /// <summary>
    /// Sample payload type used by Dapr trigger functions that bind a JSON-deserialised POCO.
    /// </summary>
    public sealed class DaprPayload
    {
        /// <summary>Gets or sets the identifier carried in the test event data.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Gets or sets a human-readable description carried in the test event data.</summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Return type used to exercise named <see cref="DaprPublishOutputAttribute"/> capture via
    /// <see cref="FunctionInvocationResult.OutputData"/>.
    /// </summary>
    public sealed class DaprPublishOutputResult
    {
        /// <summary>Gets or sets the outbound message published to the Dapr topic.</summary>
        [DaprPublishOutput(PubSubName = PubSubName, Topic = TopicName)]
        public string? OutboundMessage { get; set; }
    }
}
