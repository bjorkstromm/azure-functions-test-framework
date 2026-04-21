using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TestProject;

public partial class RabbitMQTriggerFunction
{
    /// <summary>
    /// Records a UTF-8 text message delivered through the RabbitMQ trigger.
    /// </summary>
    /// <param name="message">The message body.</param>
    [Function("ProcessRabbitMqMessage")]
    public void Run([RabbitMQTrigger("test-rabbit-queue", ConnectionStringSetting = "RabbitMQConnection")] string message)
    {
        logger.LogInformation("RabbitMQ message: {Message}", message);
        processedItems.Add(message);
    }
}
