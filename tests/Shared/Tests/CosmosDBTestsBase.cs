using AzureFunctions.TestFramework.CosmosDB;
using AzureFunctions.TestFramework.Queue;
using Xunit;

namespace TestProject;

/// <summary>
/// Tests covering CosmosDB Trigger, Input, and Output bindings.
/// <para>
/// CosmosDB output binding values are captured via <c>FunctionInvocationResult.OutputData</c>.
/// CosmosDB input binding injection uses <c>WithCosmosDBInputDocuments</c> on the builder
/// (requires <c>AzureFunctions.TestFramework.CosmosDB</c>).
/// </para>
/// </summary>
public abstract class CosmosDBTestsBase : TestHostTestBase
{
    private InMemoryProcessedItemsService? _processedItems;

    protected CosmosDBTestsBase(ITestOutputHelper output) : base(output) { }

    protected override async Task<IFunctionsTestHost> CreateTestHostAsync()
    {
        _processedItems = new InMemoryProcessedItemsService();
        return await CreateTestHostWithProcessedItemsAsync(_processedItems);
    }

    protected abstract Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(
        InMemoryProcessedItemsService processedItems);

    // -------------------------------------------------------------------------
    // CosmosDB trigger tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that a CosmosDB trigger function is invoked with a list of strongly-typed documents.
    /// </summary>
    [Fact]
    public async Task InvokeCosmosDBAsync_WithDocuments_Succeeds()
    {
        var documents = new[]
        {
            new CosmosDocument { Id = "doc-1", Title = "First document" },
            new CosmosDocument { Id = "doc-2", Title = "Second document" }
        };

        var result = await TestHost.InvokeCosmosDBAsync("ProcessCosmosDocuments", documents, TestCancellation);

        Assert.True(result.Success, $"CosmosDB invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Equal(2, processed.Count);
        Assert.Equal("doc-1", processed[0]);
        Assert.Equal("doc-2", processed[1]);
    }

    /// <summary>
    /// Verifies that a CosmosDB trigger function is invoked with a single document as a JSON string.
    /// </summary>
    [Fact]
    public async Task InvokeCosmosDBAsync_WithJsonString_Succeeds()
    {
        var json = """[{"id":"json-1","title":"From JSON"}]""";

        var result = await TestHost.InvokeCosmosDBAsync("ProcessCosmosDocuments", json, TestCancellation);

        Assert.True(result.Success, $"CosmosDB JSON invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal("json-1", processed[0]);
    }

    // -------------------------------------------------------------------------
    // CosmosDB output binding test
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that a CosmosDB trigger function with an output binding produces a captured result.
    /// </summary>
    [Fact]
    public async Task InvokeCosmosDBAsync_WithOutputBinding_CapturesDocument()
    {
        var documents = new[] { new CosmosDocument { Id = "trigger-1", Title = "Source doc" } };

        var result = await TestHost.InvokeCosmosDBAsync(
            "ProcessAndWriteCosmosDocument", documents, TestCancellation);

        Assert.True(result.Success, $"CosmosDB output binding invocation failed: {result.Error}");

        // Verify trigger side-effect
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal("trigger-1", processed[0]);

        // Verify the return value (CosmosDB output binding uses the function return value)
        var written = result.ReadReturnValueAs<CosmosDocument>();
        Assert.NotNull(written);
        Assert.Equal("output-trigger-1", written.Id);
        Assert.Equal("copy:Source doc", written.Title);
    }

    // -------------------------------------------------------------------------
    // CosmosDB input binding test
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that a queue-triggered function with a CosmosDB input binding receives the
    /// document registered via <c>WithCosmosDBInputDocuments</c>.
    /// </summary>
    [Fact]
    public async Task InvokeQueueAsync_WithCosmosDBInput_ReadsRegisteredDocument()
    {
        var result = await TestHost.InvokeQueueAsync("ReadCosmosInput", "unused", TestCancellation);

        Assert.True(result.Success, $"CosmosDB input binding invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(CosmosDBInputTestTitle, processed[0]);
    }

    /// <summary>
    /// The document title injected for the <c>[CosmosDBInput]</c> test.
    /// Must match the value registered in <see cref="CreateTestHostWithProcessedItemsAsync"/>.
    /// </summary>
    protected const string CosmosDBInputTestTitle = "hello from cosmos input!";
}
