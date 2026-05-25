using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TestProject;

public class ServiceBusBatchTriggerFunction
{
    private readonly ILogger<ServiceBusBatchTriggerFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    public ServiceBusBatchTriggerFunction(ILogger<ServiceBusBatchTriggerFunction> logger, IProcessedItemsService processedItems)
    {
        _logger = logger;
        _processedItems = processedItems;
    }

    [Function("ProcessServiceBusMessageBatch")]
    public void Run(
        [ServiceBusTrigger("test-batch-topic", "test-batch-subscription", IsBatched = true)]
        ServiceBusReceivedMessage[] messages)
    {
        foreach (var message in messages)
        {
            var body = message.Body.ToString();
            _logger.LogInformation("Processing Service Bus batch message: {Body}", body);
            _processedItems.Add(body);
        }
    }
}
