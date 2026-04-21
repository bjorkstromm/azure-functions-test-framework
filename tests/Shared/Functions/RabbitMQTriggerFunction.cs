using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TestProject;

/// <summary>
/// RabbitMQ trigger sample functions used by the integration test matrix.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="RabbitMQTriggerFunction"/> class.
/// </remarks>
/// <param name="logger">The function logger.</param>
/// <param name="processedItems">In-memory sink for asserted side effects.</param>
public partial class RabbitMQTriggerFunction(ILogger<RabbitMQTriggerFunction> logger, IProcessedItemsService processedItems)
{
    /// <summary>
    /// Sample payload type for RabbitMQ trigger functions that bind a JSON-deserialized POCO.
    /// </summary>
    public sealed class RabbitMqOrderPayload
    {
        /// <summary>
        /// Gets or sets the business identifier carried in the test message body.
        /// </summary>
        public string OrderId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Return type used to exercise named <see cref="RabbitMQOutputAttribute"/> capture via <see cref="FunctionInvocationResult.OutputData"/>.
    /// </summary>
    public sealed class RabbitMqOutputBindingResult
    {
        /// <summary>
        /// Gets or sets the outbound message body sent to the configured RabbitMQ queue.
        /// </summary>
        [RabbitMQOutput(QueueName = "rabbit-out-queue", ConnectionStringSetting = "RabbitMQConnection")]
        public string OutboundMessage { get; set; } = string.Empty;
    }
}
