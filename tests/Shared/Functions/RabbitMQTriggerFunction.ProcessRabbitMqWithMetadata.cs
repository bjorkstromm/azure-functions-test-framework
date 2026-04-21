using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TestProject;

public partial class RabbitMQTriggerFunction
{
    /// <summary>
    /// Records the message body together with selected trigger metadata from <see cref="FunctionContext.BindingContext.BindingData"/>.
    /// </summary>
    /// <param name="message">The message body.</param>
    /// <param name="context">Function execution context.</param>
    [Function("ProcessRabbitMqWithMetadata")]
    public void RunWithMetadata(
        [RabbitMQTrigger("test-rabbit-metadata-queue", ConnectionStringSetting = "RabbitMQConnection")] string message,
        FunctionContext context)
    {
        var data = context.BindingContext.BindingData;
        var routingKey = GetBindingString(data, "RoutingKey");
        var messageId = GetBindingString(data, "MessageId");
        logger.LogInformation(
            "RabbitMQ metadata message: {Message}, RoutingKey: {RoutingKey}, MessageId: {MessageId}",
            message,
            routingKey,
            messageId);
        processedItems.Add($"{message}|rk={routingKey ?? ""}|mid={messageId ?? ""}");
    }

    private static string? GetBindingString(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var raw) || raw is null)
        {
            return null;
        }

        if (raw is string s)
        {
            try
            {
                return JsonSerializer.Deserialize<string>(s) ?? s;
            }
            catch (JsonException)
            {
                return s;
            }
        }

        return raw.ToString();
    }
}
