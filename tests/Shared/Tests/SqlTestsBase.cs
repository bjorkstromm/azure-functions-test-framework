using AzureFunctions.TestFramework.Queue;
using AzureFunctions.TestFramework.Sql;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Xunit;

namespace TestProject;

/// <summary>
/// Tests covering Azure SQL Trigger, Input, and Output bindings.
/// <para>
/// SQL output binding values are captured via <c>FunctionInvocationResult.OutputData</c>.
/// SQL input binding injection uses <c>WithSqlInputRows</c> on the builder
/// (requires <c>AzureFunctions.TestFramework.Sql</c>).
/// </para>
/// </summary>
public abstract class SqlTestsBase : TestHostTestBase
{
    private InMemoryProcessedItemsService? _processedItems;

    protected SqlTestsBase(ITestOutputHelper output) : base(output) { }

    protected override async Task<IFunctionsTestHost> CreateTestHostAsync()
    {
        _processedItems = new InMemoryProcessedItemsService();
        return await CreateTestHostWithProcessedItemsAsync(_processedItems);
    }

    protected abstract Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(
        InMemoryProcessedItemsService processedItems);

    // -------------------------------------------------------------------------
    // SQL trigger tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that a SQL trigger function is invoked with a list of strongly-typed changes.
    /// </summary>
    [Fact]
    public async Task InvokeSqlAsync_WithChanges_Succeeds()
    {
        var changes = new[]
        {
            new SqlChange<SqlProduct>(SqlChangeOperation.Insert, new SqlProduct { Id = 1, Name = "Widget" }),
            new SqlChange<SqlProduct>(SqlChangeOperation.Update, new SqlProduct { Id = 2, Name = "Gadget" })
        };

        var result = await TestHost.InvokeSqlAsync("ProcessSqlChanges", changes, TestCancellation);

        Assert.True(result.Success, $"SQL invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Equal(2, processed.Count);
        Assert.Equal("Insert:1", processed[0]);
        Assert.Equal("Update:2", processed[1]);
    }

    /// <summary>
    /// Verifies that a SQL trigger function is invoked with a raw JSON string.
    /// </summary>
    [Fact]
    public async Task InvokeSqlAsync_WithJsonString_Succeeds()
    {
        var json = """[{"operation":0,"item":{"id":10,"name":"FromJson"}}]""";

        var result = await TestHost.InvokeSqlAsync("ProcessSqlChanges", json, TestCancellation);

        Assert.True(result.Success, $"SQL JSON invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal("Insert:10", processed[0]);
    }

    // -------------------------------------------------------------------------
    // SQL output binding test
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that a SQL trigger function with an output binding produces a captured result.
    /// </summary>
    [Fact]
    public async Task InvokeSqlAsync_WithOutputBinding_CapturesRow()
    {
        var changes = new[]
        {
            new SqlChange<SqlProduct>(SqlChangeOperation.Insert, new SqlProduct { Id = 1, Name = "Widget" })
        };

        var result = await TestHost.InvokeSqlAsync(
            "ProcessAndWriteSqlChange", changes, TestCancellation);

        Assert.True(result.Success, $"SQL output binding invocation failed: {result.Error}");

        // Verify trigger side-effect
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal("Insert:1", processed[0]);

        // Verify the return value (SQL output binding uses the function return value)
        var written = result.ReadReturnValueAs<SqlProduct>();
        Assert.NotNull(written);
        Assert.Equal(1, written.Id);
        Assert.Equal("copy:Widget", written.Name);
    }

    // -------------------------------------------------------------------------
    // SQL input binding test
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that a queue-triggered function with a SQL input binding receives the
    /// rows registered via <c>WithSqlInputRows</c>.
    /// </summary>
    [Fact]
    public async Task InvokeQueueAsync_WithSqlInput_ReadsRegisteredRows()
    {
        var result = await TestHost.InvokeQueueAsync("ReadSqlInput", "unused", TestCancellation);

        Assert.True(result.Success, $"SQL input binding invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(SqlInputTestName, processed[0]);
    }

    /// <summary>
    /// The product name injected for the <c>[SqlInput]</c> test.
    /// Must match the value registered in <see cref="CreateTestHostWithProcessedItemsAsync"/>.
    /// </summary>
    protected const string SqlInputTestName = "hello from sql input!";
}
