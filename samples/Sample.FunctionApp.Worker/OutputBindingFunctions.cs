using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Sample.FunctionApp.Worker;

/// <summary>
/// Example functions used to validate non-HTTP return values and output bindings.
/// </summary>
public class OutputBindingFunctions
{
    private readonly ILogger<OutputBindingFunctions> _logger;

    public OutputBindingFunctions(ILogger<OutputBindingFunctions> logger)
    {
        _logger = logger;
    }

    [Function("ReturnQueueMessageValue")]
    public string ReturnQueueMessageValue([QueueTrigger("return-value-queue")] string message)
    {
        _logger.LogInformation("Returning queue message value for {Message}", message);
        return $"return:{message}";
    }

    [Function("CreateQueueOutputMessages")]
    public QueueOutputBindingResult CreateQueueOutputMessages([QueueTrigger("output-binding-queue")] string message)
    {
        _logger.LogInformation("Creating queue output messages for {Message}", message);
        return new QueueOutputBindingResult
        {
            Messages =
            [
                $"output:{message}",
                $"output:{message}:copy"
            ]
        };
    }
}

public sealed class QueueOutputBindingResult
{
    [QueueOutput("captured-output-queue")]
    public string[] Messages { get; set; } = [];
}
