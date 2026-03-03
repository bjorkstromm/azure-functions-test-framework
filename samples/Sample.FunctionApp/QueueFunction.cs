using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Sample.FunctionApp;

/// <summary>
/// Example queue-triggered function. Processes messages from a storage queue.
/// In tests, invoke via <c>host.InvokeQueueAsync("ProcessQueueMessage", queueMessage)</c>.
/// </summary>
public class QueueFunction
{
    private readonly ILogger<QueueFunction> _logger;

    public QueueFunction(ILogger<QueueFunction> logger)
    {
        _logger = logger;
    }

    [Function("ProcessQueueMessage")]
    public void Run([QueueTrigger("test-queue")] string message)
    {
        _logger.LogInformation("Processing queue message: {Message}", message);
    }
}
