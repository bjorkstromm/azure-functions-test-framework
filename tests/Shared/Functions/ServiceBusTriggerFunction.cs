using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TestProject;

public class ServiceBusTriggerFunction
{
    private readonly ILogger<ServiceBusTriggerFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    public ServiceBusTriggerFunction(ILogger<ServiceBusTriggerFunction> logger, IProcessedItemsService processedItems)
    {
        _logger = logger;
        _processedItems = processedItems;
    }

    [Function("ProcessServiceBusMessage")]
    public void Run([ServiceBusTrigger("test-topic", "test-subscription")] string message)
    {
        _logger.LogInformation("Processing Service Bus message: {Body}", message);
        _processedItems.Add(message);
    }

    [Function("ProcessServiceBusBytes")]
    public void RunBytes([ServiceBusTrigger("test-bytes")] byte[] message)
        => _processedItems.Add(Convert.ToBase64String(message));

    [Function("ProcessServiceBusPayload")]
    public void RunPayload([ServiceBusTrigger("test-payload")] ServiceBusPayload message)
        => _processedItems.Add($"{message.OrderName}:{message.Quantity}");
}

public sealed class ServiceBusPayload
{
    public string OrderName { get; set; } = "";
    public int Quantity { get; set; }
}
