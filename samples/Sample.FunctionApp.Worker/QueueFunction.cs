using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Sample.FunctionApp.Worker;

/// <summary>
/// Example queue-triggered function. In tests, invoke via <c>host.InvokeQueueAsync("ProcessQueueMessage", message)</c>.
/// </summary>
public class QueueFunction
{
    private readonly ILogger<QueueFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public QueueFunction(ILogger<QueueFunction> logger, IProcessedItemsService processedItems)
    {
        _logger = logger;
        _processedItems = processedItems;
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Function("ProcessQueueMessage")]
    public void Run([QueueTrigger("test-queue")] string message)
    {
        _logger.LogInformation("Processing queue message: {Message}", message);
        _processedItems.Add(message);
    }
}
