using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Sample.FunctionApp.Worker;

/// <summary>
/// Example Service Bus–triggered function. In tests, invoke via
/// <c>host.InvokeServiceBusAsync("ProcessServiceBusMessage", message)</c>.
/// </summary>
public class ServiceBusFunction
{
    private readonly ILogger<ServiceBusFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    public ServiceBusFunction(ILogger<ServiceBusFunction> logger, IProcessedItemsService processedItems)
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
}
