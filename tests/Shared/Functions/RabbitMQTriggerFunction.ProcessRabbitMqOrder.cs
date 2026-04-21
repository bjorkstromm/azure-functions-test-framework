using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TestProject;

public partial class RabbitMQTriggerFunction
{
    /// <summary>
    /// Records an order identifier from a JSON POCO body.
    /// </summary>
    /// <param name="order">The deserialized order payload.</param>
    [Function("ProcessRabbitMqOrder")]
    public void RunOrder(
        [RabbitMQTrigger("test-rabbit-order-queue", ConnectionStringSetting = "RabbitMQConnection")]
        RabbitMqOrderPayload order)
    {
        logger.LogInformation("RabbitMQ order: {OrderId}", order.OrderId);
        processedItems.Add(order.OrderId);
    }
}
