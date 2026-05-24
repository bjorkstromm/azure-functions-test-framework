using AzureFunctions.TestFramework.Dapr;

namespace TestProject;

/// <summary>Tests for Dapr-triggered functions.</summary>
public abstract class DaprTriggerTestsBase(ITestOutputHelper output) : TestHostTestBase(output)
{
    private InMemoryProcessedItemsService? _processedItems;

    protected override async Task<IFunctionsTestHost> CreateTestHostAsync()
    {
        _processedItems = new InMemoryProcessedItemsService();
        return await CreateTestHostWithProcessedItemsAsync(_processedItems);
    }

    protected abstract Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(InMemoryProcessedItemsService processedItems);

    // -------------------------------------------------------------------------
    // DaprBindingTrigger — string overload
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeDaprBindingAsync_WithString_Succeeds()
    {
        const string data = "hello from dapr binding!";

        var result = await TestHost.InvokeDaprBindingAsync("ProcessDaprBinding", data, TestCancellation);

        Assert.True(result.Success, $"Dapr binding invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(data, processed[0]);
    }

    // -------------------------------------------------------------------------
    // DaprBindingTrigger — POCO overload
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeDaprBindingAsync_WithPoco_Succeeds()
    {
        var payload = new DaprTriggerFunction.DaprPayload { Id = "evt-42", Message = "test-message" };

        var result = await TestHost.InvokeDaprBindingAsync("ProcessDaprBindingPayload", payload, cancellationToken: TestCancellation);

        Assert.True(result.Success, $"Dapr binding POCO invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal("evt-42", processed[0]);
    }

    // -------------------------------------------------------------------------
    // DaprServiceInvocationTrigger — string overload
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeDaprServiceInvocationAsync_WithString_Succeeds()
    {
        const string body = "service-invocation-body";

        var result = await TestHost.InvokeDaprServiceInvocationAsync("ProcessDaprInvocation", body, TestCancellation);

        Assert.True(result.Success, $"Dapr service invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(body, processed[0]);
    }

    // -------------------------------------------------------------------------
    // DaprTopicTrigger — string overload
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeDaprTopicAsync_WithString_Succeeds()
    {
        const string message = "hello from dapr pub/sub!";

        var result = await TestHost.InvokeDaprTopicAsync("ProcessDaprTopic", message, TestCancellation);

        Assert.True(result.Success, $"Dapr topic invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(message, processed[0]);
    }

    // -------------------------------------------------------------------------
    // DaprPublishOutput — verify function executes successfully
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that a function with a <c>[DaprPublishOutput]</c> return property executes without error.
    /// <para>
    /// Output binding data capture via <c>FunctionInvocationResult.OutputData</c> is not verified
    /// here because the Dapr extension's source generator (v1.0.1) emits the <c>daprPublish</c>
    /// output binding with <c>direction: "In"</c> instead of <c>direction: "Out"</c>, so the
    /// worker SDK does not populate <c>OutputData</c> for it. The invocation still succeeds.
    /// </para>
    /// </summary>
    [Fact]
    public async Task InvokeDaprBindingAsync_WithOutputBinding_Succeeds()
    {
        const string data = "dapr-output-trigger";

        var result = await TestHost.InvokeDaprBindingAsync("ReturnDaprPublishOutput", data, TestCancellation);

        Assert.True(result.Success, $"Dapr output binding invocation failed: {result.Error}");
    }
}
