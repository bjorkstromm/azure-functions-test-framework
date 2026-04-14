using AzureFunctions.TestFramework.Queue;
using AzureFunctions.TestFramework.Tables;
using Xunit;

namespace TestProject;

/// <summary>
/// Tests covering <c>[TableInput]</c> and <c>[TableOutput]</c> bindings.
/// <para>
/// Table output binding values are captured generically via <c>FunctionInvocationResult.OutputData</c>
/// without requiring the Tables package.  Table input binding injection requires
/// <c>AzureFunctions.TestFramework.Tables</c> via <c>WithTableEntity</c> /
/// <c>WithTableEntities</c> on the builder.
/// </para>
/// </summary>
public abstract class TablesTestsBase : TestHostTestBase
{
    /// <summary>Expected payload injected into the fake table entity for input binding tests.</summary>
    protected const string TableEntityTestPayload = "hello from table input!";

    private InMemoryProcessedItemsService? _processedItems;

    protected TablesTestsBase(ITestOutputHelper output) : base(output) { }

    protected override async Task<IFunctionsTestHost> CreateTestHostAsync()
    {
        _processedItems = new InMemoryProcessedItemsService();
        return await CreateTestHostWithProcessedItemsAsync(_processedItems);
    }

    protected abstract Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(InMemoryProcessedItemsService processedItems);

    // -------------------------------------------------------------------------
    // Table input tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// Invokes a queue-triggered function that has a <c>[TableInput]</c> parameter.
    /// The concrete test class registers a fake entity via <c>WithTableEntity</c> on the host
    /// builder; this test verifies the entity's payload is surfaced inside the function.
    /// </summary>
    [Fact]
    public async Task InvokeQueueAsync_WithTableInput_ReadsRegisteredEntity()
    {
        var result = await TestHost.InvokeQueueAsync("ReadTableEntity", "unused", TestCancellation);

        Assert.True(result.Success, $"Table input invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(TableEntityTestPayload, processed[0]);
    }

}
