using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TestProject;

public class ServiceBusReceivedMessageTriggerFunction
{
    private readonly ILogger<ServiceBusReceivedMessageTriggerFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    public ServiceBusReceivedMessageTriggerFunction(ILogger<ServiceBusReceivedMessageTriggerFunction> logger, IProcessedItemsService processedItems)
    {
        _logger = logger;
        _processedItems = processedItems;
    }

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
