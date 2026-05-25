using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TestProject;

/// <summary>
/// Functions that exercise CosmosDB trigger, input, and output bindings.
/// </summary>
public class CosmosDBFunction
{
    /// <summary>The CosmosDB database name used for trigger and input bindings.</summary>
    public const string DatabaseName = "TestDatabase";

    /// <summary>The CosmosDB container name used for trigger bindings.</summary>
    public const string ContainerName = "TestContainer";

    /// <summary>The CosmosDB container name used for input bindings.</summary>
    public const string InputContainerName = "InputContainer";

    /// <summary>The CosmosDB lease container name (required by the trigger).</summary>
    public const string LeaseContainerName = "leases";

    private readonly ILogger<CosmosDBFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    public CosmosDBFunction(ILogger<CosmosDBFunction> logger, IProcessedItemsService processedItems)
    {
        _logger = logger;
        _processedItems = processedItems;
    }

    /// <summary>
    /// CosmosDB trigger function that processes changed documents from the change feed.
    /// The function parameter receives the changed documents as a JSON-deserialized list.
    /// </summary>
    [Function("ProcessCosmosDocuments")]
    public void Run(
        [CosmosDBTrigger(
            databaseName: DatabaseName,
            containerName: ContainerName,
            Connection = "CosmosDBConnection",
            LeaseContainerName = LeaseContainerName)] IReadOnlyList<CosmosDocument> documents)
    {
        _logger.LogInformation("Processing {Count} CosmosDB document(s)", documents.Count);
        foreach (var doc in documents)
        {
            _processedItems.Add(doc.Id ?? string.Empty);
        }
    }

    /// <summary>
    /// CosmosDB trigger function that writes a document to a CosmosDB output binding.
    /// </summary>
    [Function("ProcessAndWriteCosmosDocument")]
    [CosmosDBOutput(DatabaseName, ContainerName + "-output", Connection = "CosmosDBConnection")]
    public CosmosDocument? ProcessAndWrite(
        [CosmosDBTrigger(
            databaseName: DatabaseName,
            containerName: ContainerName,
            Connection = "CosmosDBConnection",
            LeaseContainerName = LeaseContainerName)] IReadOnlyList<CosmosDocument> documents)
    {
        _logger.LogInformation("Processing and writing {Count} CosmosDB document(s)", documents.Count);
        var first = documents.FirstOrDefault();
        if (first is not null)
        {
            _processedItems.Add(first.Id ?? string.Empty);
            return new CosmosDocument { Id = $"output-{first.Id}", Title = $"copy:{first.Title}" };
        }
        return null;
    }

    /// <summary>
    /// Queue-triggered function that reads a CosmosDB input binding and records the document.
    /// </summary>
    [Function("ReadCosmosInput")]
    public void ReadCosmosInput(
        [QueueTrigger("cosmos-input-queue")] string unused,
        [CosmosDBInput(
            databaseName: DatabaseName,
            containerName: InputContainerName,
            Connection = "CosmosDBConnection",
            Id = "test-id",
            PartitionKey = "test-pk")] CosmosDocument? document)
    {
        if (document is not null)
        {
            _logger.LogInformation("Read CosmosDB input document: {Title}", document.Title);
            _processedItems.Add(document.Title ?? string.Empty);
        }
        else
        {
            _processedItems.Add("null");
        }
    }
}

/// <summary>A simple CosmosDB document model used in tests.</summary>
public sealed class CosmosDocument
{
    public string? Id { get; set; }
    public string? Title { get; set; }
}
