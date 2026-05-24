using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Kusto;
using Microsoft.Extensions.Logging;

namespace TestProject;

/// <summary>
/// Functions that exercise Azure Data Explorer (Kusto) input and output bindings.
/// </summary>
public class DataExplorerFunction
{
    /// <summary>The Kusto database name used by input and output bindings.</summary>
    public const string DatabaseName = "TestDatabase";

    /// <summary>The Kusto table name used for input binding tests.</summary>
    public const string InputTableName = "InputTable";

    /// <summary>The Kusto output table name used for output binding tests.</summary>
    public const string OutputTableName = "OutputTable";

    /// <summary>The KQL command used by the <c>[KustoInput]</c> test.</summary>
    public const string InputKqlCommand = InputTableName + " | take 5";

    private readonly ILogger<DataExplorerFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public DataExplorerFunction(ILogger<DataExplorerFunction> logger, IProcessedItemsService processedItems)
    {
        _logger = logger;
        _processedItems = processedItems;
    }

    /// <summary>
    /// Queue-triggered function that reads a Kusto input binding and records each row.
    /// </summary>
    [Function("ReadKustoInput")]
    public void ReadKustoInput(
        [QueueTrigger("kusto-input-queue")] string unused,
        [KustoInput(DatabaseName, KqlCommand = InputKqlCommand, Connection = "KustoConnection")]
        IReadOnlyList<KustoRow>? rows)
    {
        if (rows is null || rows.Count == 0)
        {
            _processedItems.Add("null");
            return;
        }

        foreach (var row in rows)
        {
            _logger.LogInformation("Read Kusto row: {Name}", row.Name);
            _processedItems.Add(row.Name ?? string.Empty);
        }
    }

    /// <summary>
    /// Queue-triggered function that writes a row to a Kusto output binding.
    /// </summary>
    [Function("ProcessAndWriteKustoOutput")]
    [KustoOutput(DatabaseName, TableName = OutputTableName, Connection = "KustoConnection")]
    public KustoRow ProcessAndWriteKustoOutput([QueueTrigger("kusto-output-queue")] string name)
    {
        _logger.LogInformation("Writing Kusto output row for {Name}", name);
        _processedItems.Add(name);
        return new KustoRow { Id = 1, Name = $"copy:{name}" };
    }
}

/// <summary>A simple Kusto row model used in tests.</summary>
public sealed class KustoRow
{
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string? Name { get; set; }
}
