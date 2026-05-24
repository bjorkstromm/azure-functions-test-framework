using AzureFunctions.TestFramework.DataExplorer;
using AzureFunctions.TestFramework.Queue;
using Xunit;

namespace TestProject;

/// <summary>
/// Tests covering Azure Data Explorer (Kusto) input and output bindings.
/// <para>
/// Kusto output binding values are captured via <c>FunctionInvocationResult.OutputData</c>.
/// Kusto input binding injection uses <c>WithKustoInputRows</c> on the builder
/// (requires <c>AzureFunctions.TestFramework.DataExplorer</c>).
/// </para>
/// </summary>
public abstract class DataExplorerTestsBase : TestHostTestBase
{
    private InMemoryProcessedItemsService? _processedItems;

    protected DataExplorerTestsBase(ITestOutputHelper output) : base(output) { }

    protected override async Task<IFunctionsTestHost> CreateTestHostAsync()
    {
        _processedItems = new InMemoryProcessedItemsService();
        return await CreateTestHostWithProcessedItemsAsync(_processedItems);
    }

    protected abstract Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(
        InMemoryProcessedItemsService processedItems);

    /// <summary>
    /// Verifies that a queue-triggered function with a Kusto input binding receives
    /// the rows registered via <c>WithKustoInputRows</c>.
    /// </summary>
    [Fact]
    public async Task InvokeQueueAsync_WithKustoInput_ReadsRegisteredRows()
    {
        var result = await TestHost.InvokeQueueAsync("ReadKustoInput", "unused", TestCancellation);

        Assert.True(result.Success, $"Kusto input binding invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(KustoInputTestName, processed[0]);
    }

    /// <summary>
    /// Verifies that a queue-triggered function with a Kusto output binding produces a captured row.
    /// </summary>
    [Fact]
    public async Task InvokeQueueAsync_WithKustoOutput_CapturesRow()
    {
        var result = await TestHost.InvokeQueueAsync("ProcessAndWriteKustoOutput", "source-row", TestCancellation);

        Assert.True(result.Success, $"Kusto output binding invocation failed: {result.Error}");

        // Verify trigger side-effect
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal("source-row", processed[0]);

        // Verify the return value (Kusto output binding uses the function return value)
        var written = result.ReadReturnValueAs<KustoRow>();
        Assert.NotNull(written);
        Assert.Equal(1, written.Id);
        Assert.Equal("copy:source-row", written.Name);
    }

    /// <summary>
    /// The row name injected for the <c>[KustoInput]</c> test.
    /// Must match the value registered in <see cref="CreateTestHostWithProcessedItemsAsync"/>.
    /// </summary>
    protected const string KustoInputTestName = "hello from kusto input!";
}
