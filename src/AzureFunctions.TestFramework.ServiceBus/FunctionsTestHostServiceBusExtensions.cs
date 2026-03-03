using System.Text;
using System.Text.Json;
using AzureFunctions.TestFramework.Core;
using Azure.Messaging.ServiceBus;

namespace AzureFunctions.TestFramework.ServiceBus;

/// <summary>
/// Extension methods for invoking Service Bus–triggered Azure Functions via <see cref="IFunctionsTestHost"/>.
/// </summary>
public static class FunctionsTestHostServiceBusExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Invokes a Service Bus–triggered function by name.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the Service Bus function (case-insensitive).</param>
    /// <param name="message">
    /// The <see cref="ServiceBusMessage"/> to simulate as the incoming trigger message.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeServiceBusAsync(
        this IFunctionsTestHost host,
        string functionName,
        ServiceBusMessage message,
        CancellationToken cancellationToken = default)
    {
        var bodyBytes = message.Body?.ToArray() ?? Array.Empty<byte>();

        // Build trigger metadata so the worker can bind ServiceBusReceivedMessage parameters.
        var metadata = new
        {
            MessageId = message.MessageId,
            CorrelationId = message.CorrelationId,
            ContentType = message.ContentType,
            Subject = message.Subject,
            SessionId = message.SessionId,
            ReplyTo = message.ReplyTo,
            ReplyToSessionId = message.ReplyToSessionId,
            To = message.To,
            PartitionKey = message.PartitionKey,
            TimeToLiveTotalSeconds = message.TimeToLive.TotalSeconds,
            ScheduledEnqueueTime = message.ScheduledEnqueueTime,
        };
        var triggerMetadataJson = JsonSerializer.Serialize(metadata, _jsonOptions);

        var context = new FunctionInvocationContext
        {
            TriggerType = "serviceBusTrigger",
            InputData =
            {
                ["$messageBodyBytes"] = bodyBytes,
                ["$triggerMetadata"] = triggerMetadataJson
            }
        };

        return host.Invoker.InvokeAsync(functionName, context, cancellationToken);
    }
}
