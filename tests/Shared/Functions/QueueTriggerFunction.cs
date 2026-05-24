using Azure;
using Azure.Data.Tables;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class QueueTriggerFunction
{
    private readonly ILogger<QueueTriggerFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public QueueTriggerFunction(ILogger<QueueTriggerFunction> logger, IProcessedItemsService processedItems)
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

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Function("ProcessQueueMessageTyped")]
    public void RunTyped([QueueTrigger("test-typed-queue")] QueueMessage message)
    {
        _logger.LogInformation("Processing typed queue message: {MessageId}", message.MessageId);
        _processedItems.Add(message.Body.ToString());
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Function("ReturnQueueMessageValue")]
    public string ReturnQueueMessageValue([QueueTrigger("return-value-queue")] string message)
    {
        _logger.LogInformation("Returning queue message value for {Message}", message);
        return $"return:{message}";
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Function("CreateQueueOutputMessages")]
    public QueueOutputBindingResult CreateQueueOutputMessages([QueueTrigger("output-binding-queue")] string message)
    {
        _logger.LogInformation("Creating queue output messages for {Message}", message);
        return new QueueOutputBindingResult { Messages = [$"output:{message}", $"output:{message}:copy"] };
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Function("CreateBlobOutputDocument")]
    public BlobOutputBindingResult CreateBlobOutputDocument([QueueTrigger("blob-output-binding-queue")] string message)
    {
        _logger.LogInformation("Creating blob output document for {Message}", message);
        return new BlobOutputBindingResult { Content = $"blob:{message}" };
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
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

/// <summary>
/// Represents this type.
/// </summary>
public sealed class QueueOutputBindingResult
{
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    [QueueOutput("captured-output-queue")]
    public string[] Messages { get; set; } = [];
}

/// <summary>
/// Represents this type.
/// </summary>
public sealed class BlobOutputBindingResult
{
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    [BlobOutput("captured-output/blob-output.txt")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Represents this type.
/// </summary>
public sealed class TableOutputBindingResult
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    [TableOutput("CapturedOutputTable")]
    public CapturedTableEntity Entity { get; set; } = new();
}

/// <summary>
/// Represents this type.
/// </summary>
public sealed class CapturedTableEntity : ITableEntity
{
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string PartitionKey { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string RowKey { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public DateTimeOffset? Timestamp { get; set; }
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public ETag ETag { get; set; }
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string Payload { get; set; } = string.Empty;
}
