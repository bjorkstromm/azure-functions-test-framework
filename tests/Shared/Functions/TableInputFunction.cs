using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TestProject;

/// <summary>
/// Functions that exercise <c>[TableInput]</c> bindings. Invoked via a queue trigger so
/// the table input data can be injected through <c>ISyntheticBindingProvider</c>.
/// </summary>
public class TableInputFunction
{
    /// <summary>
    /// The table name used in <see cref="ReadTableEntity"/>.
    /// Must match the value passed to <c>WithTableEntity</c> in the test host builder.
    /// </summary>
    public const string TableName = "TestInputTable";

    /// <summary>The partition key used in <see cref="ReadTableEntity"/>.</summary>
    public const string PartitionKey = "TestPK";

    /// <summary>The row key used in <see cref="ReadTableEntity"/>.</summary>
    public const string RowKey = "TestRK";

    private readonly ILogger<TableInputFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    public TableInputFunction(ILogger<TableInputFunction> logger, IProcessedItemsService processedItems)
    {
        _logger = logger;
        _processedItems = processedItems;
    }

    /// <summary>
    /// Queue-triggered function that reads a single table entity via <c>[TableInput]</c>
    /// and records its payload in the processed-items service.
    /// </summary>
    [Function("ReadTableEntity")]
    public void ReadTableEntity(
        [QueueTrigger("read-table-entity-queue")] string unused,
        [TableInput(TableName, PartitionKey, RowKey)] CapturedTableEntity entity)
    {
        _logger.LogInformation(
            "Read table entity: {PK}/{RK} = {Payload}",
            entity.PartitionKey, entity.RowKey, entity.Payload);
        _processedItems.Add(entity.Payload);
    }
}
