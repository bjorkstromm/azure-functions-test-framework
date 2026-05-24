using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Extensions.Logging;

namespace TestProject;

/// <summary>
/// Functions that exercise SQL trigger, input, and output bindings.
/// </summary>
public class SqlFunction
{
    /// <summary>The SQL table name used for trigger bindings.</summary>
    public const string TableName = "Products";

    /// <summary>The SQL table name used for the output binding.</summary>
    public const string OutputTableName = "ProductsOutput";

    /// <summary>The SQL command text used for input bindings.</summary>
    public const string InputCommandText = "SELECT * FROM Products";

    /// <summary>The connection string setting name used by all SQL bindings.</summary>
    public const string ConnectionString = "SqlConnection";

    private readonly ILogger<SqlFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    public SqlFunction(ILogger<SqlFunction> logger, IProcessedItemsService processedItems)
    {
        _logger = logger;
        _processedItems = processedItems;
    }

    /// <summary>
    /// SQL trigger function that processes changed rows from SQL change tracking.
    /// </summary>
    [Function("ProcessSqlChanges")]
    public void Run(
        [SqlTrigger(
            tableName: TableName,
            connectionStringSetting: ConnectionString)] IReadOnlyList<SqlChange<SqlProduct>> changes)
    {
        _logger.LogInformation("Processing {Count} SQL change(s)", changes.Count);
        foreach (var change in changes)
        {
            _processedItems.Add($"{change.Operation}:{change.Item.Id}");
        }
    }

    /// <summary>
    /// SQL trigger function that writes a row to a SQL output binding.
    /// </summary>
    [Function("ProcessAndWriteSqlChange")]
    [SqlOutput(commandText: OutputTableName, connectionStringSetting: ConnectionString)]
    public SqlProduct? ProcessAndWrite(
        [SqlTrigger(
            tableName: TableName,
            connectionStringSetting: ConnectionString)] IReadOnlyList<SqlChange<SqlProduct>> changes)
    {
        _logger.LogInformation("Processing and writing {Count} SQL change(s)", changes.Count);
        var first = changes.FirstOrDefault();
        if (first is not null)
        {
            _processedItems.Add($"{first.Operation}:{first.Item.Id}");
            return new SqlProduct { Id = first.Item.Id, Name = $"copy:{first.Item.Name}" };
        }
        return null;
    }

    /// <summary>
    /// Queue-triggered function that reads a SQL input binding and records each row.
    /// </summary>
    [Function("ReadSqlInput")]
    public void ReadSqlInput(
        [QueueTrigger("sql-input-queue")] string unused,
        [SqlInput(
            commandText: InputCommandText,
            connectionStringSetting: ConnectionString)] IEnumerable<SqlProduct> products)
    {
        foreach (var product in products)
        {
            _logger.LogInformation("Read SQL row: {Name}", product.Name);
            _processedItems.Add(product.Name ?? string.Empty);
        }
    }
}

/// <summary>A simple SQL row model used in tests.</summary>
public sealed class SqlProduct
{
    public int Id { get; set; }
    public string? Name { get; set; }
}
