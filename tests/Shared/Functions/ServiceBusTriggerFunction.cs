using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class ServiceBusTriggerFunction
{
    private readonly ILogger<ServiceBusTriggerFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public ServiceBusTriggerFunction(ILogger<ServiceBusTriggerFunction> logger, IProcessedItemsService processedItems)
    {
        _logger = logger;
        _processedItems = processedItems;
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Function("ProcessServiceBusMessage")]
    public void Run([ServiceBusTrigger("test-topic", "test-subscription")] string message)
    {
        _logger.LogInformation("Processing Service Bus message: {Body}", message);
        _processedItems.Add(message);
    }
}
