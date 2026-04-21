using Microsoft.Azure.Functions.Worker;

namespace TestProject;

public partial class RabbitMQTriggerFunction
{
    /// <summary>
    /// Returns a POCO whose property carries the <c>RabbitMQ</c> output binding payload.
    /// </summary>
    /// <param name="message">The inbound trigger body.</param>
    /// <returns>The outbound binding result.</returns>
    [Function("ReturnRabbitMqWithOutput")]
    public RabbitMqOutputBindingResult ReturnRabbitMqWithOutput(
        [RabbitMQTrigger("rabbit-output-in-queue", ConnectionStringSetting = "RabbitMQConnection")] string message)
    {
        return new RabbitMqOutputBindingResult { OutboundMessage = $"rabbit-out:{message}" };
    }
}
