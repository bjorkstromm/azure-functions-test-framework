using Microsoft.Azure.Functions.Worker;

namespace TestProject;

public partial class RabbitMQTriggerFunction
{
    /// <summary>Records raw RabbitMQ bytes and optional delivery metadata.</summary>
    [Function("ProcessRabbitMqBinary")]
    public void ProcessRabbitMqBinary(
        [RabbitMQTrigger("test-rabbit-binary-queue", ConnectionStringSetting = "RabbitMQConnection")] byte[] body,
        FunctionContext context)
    {
        var data = context.BindingContext.BindingData;
        processedItems.Add($"{Convert.ToBase64String(body)}|rk={GetBindingString(data, "RoutingKey") ?? ""}|mid={GetBindingString(data, "MessageId") ?? ""}");
    }
}
