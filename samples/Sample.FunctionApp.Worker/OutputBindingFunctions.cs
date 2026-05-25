using Azure;
using Azure.Data.Tables;
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

    [Function("CreateBlobOutputDocument")]
    public BlobOutputBindingResult CreateBlobOutputDocument([QueueTrigger("blob-output-binding-queue")] string message)
    {
        _logger.LogInformation("Creating blob output document for {Message}", message);
        return new BlobOutputBindingResult
        {
            Content = $"blob:{message}"
        };
    }

    [Function("CreateTableOutputEntity")]
    public TableOutputBindingResult CreateTableOutputEntity([QueueTrigger("table-output-binding-queue")] string message)
    {
        _logger.LogInformation("Creating table output entity for {Message}", message);
        return new TableOutputBindingResult
        {
            Entity = new CapturedTableEntity
            {
                PartitionKey = "captured",
                RowKey = $"row-{message.Length}",
                Payload = $"table:{message}"
            }
        };
    }
}

public sealed class QueueOutputBindingResult
{
    [QueueOutput("captured-output-queue")]
    public string[] Messages { get; set; } = [];
}

public sealed class BlobOutputBindingResult
{
    [BlobOutput("captured-output/blob-output.txt")]
    public string Content { get; set; } = string.Empty;
}

public sealed class TableOutputBindingResult
{
    [TableOutput("CapturedOutputTable")]
    public CapturedTableEntity Entity { get; set; } = new();
}

public sealed class CapturedTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;

    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public string Payload { get; set; } = string.Empty;
}
