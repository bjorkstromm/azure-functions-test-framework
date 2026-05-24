using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class ServiceBusReceivedMessageTriggerFunction
{
    private readonly ILogger<ServiceBusReceivedMessageTriggerFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public ServiceBusReceivedMessageTriggerFunction(ILogger<ServiceBusReceivedMessageTriggerFunction> logger, IProcessedItemsService processedItems)
    {
        _logger = logger;
        _processedItems = processedItems;
    }

    /// <summary>
    /// Represents this member.
    /// </summary>
    [Function("ProcessServiceBusReceivedMessage")]
    public void Run(
        [ServiceBusTrigger("test-received-topic", "test-received-subscription")]
        ServiceBusReceivedMessage message)
    {
        var body = message.Body.ToString();
        _logger.LogInformation("Processing ServiceBusReceivedMessage: {Body}, MessageId={MessageId}", body, message.MessageId);
        _processedItems.Add(body);
    }
}
