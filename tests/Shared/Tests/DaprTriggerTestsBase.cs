using System.Text.Json;
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

    [Theory]
    [InlineData("binding", false, false)]
    [InlineData("binding", true, false)]
    [InlineData("binding", false, true)]
    [InlineData("binding", true, true)]
    [InlineData("invocation", false, false)]
    [InlineData("invocation", true, false)]
    [InlineData("invocation", false, true)]
    [InlineData("invocation", true, true)]
    [InlineData("topic", false, false)]
    [InlineData("topic", true, false)]
    [InlineData("topic", false, true)]
    [InlineData("topic", true, true)]
    public async Task InvokeDaprAsync_RawPayload_PreservesBytes(string trigger, bool binaryData, bool empty)
    {
        byte[] bytes = empty ? [] : [0, 255, 128, 195, 40, 0, 42];
        var body = new BinaryData(bytes);
        var result = (trigger, binaryData) switch
        {
            ("binding", false) => await TestHost.InvokeDaprBindingAsync("ProcessDaprBindingBinary", bytes, cancellationToken: TestCancellation),
            ("binding", true) => await TestHost.InvokeDaprBindingAsync("ProcessDaprBindingBinary", body, cancellationToken: TestCancellation),
            ("invocation", false) => await TestHost.InvokeDaprServiceInvocationAsync("ProcessDaprInvocationBinary", bytes, cancellationToken: TestCancellation),
            ("invocation", true) => await TestHost.InvokeDaprServiceInvocationAsync("ProcessDaprInvocationBinary", body, cancellationToken: TestCancellation),
            ("topic", false) => await TestHost.InvokeDaprTopicAsync("ProcessDaprTopicBinary", bytes, cancellationToken: TestCancellation),
            _ => await TestHost.InvokeDaprTopicAsync("ProcessDaprTopicBinary", body, cancellationToken: TestCancellation)
        };

        Assert.True(result.Success, $"Dapr binary invocation failed: {result.Error}");
        Assert.Equal(Convert.ToBase64String(bytes), Assert.Single(_processedItems!.TakeAll()));
    }

    [Theory]
    [InlineData("binding", false)]
    [InlineData("binding", true)]
    [InlineData("invocation", false)]
    [InlineData("invocation", true)]
    [InlineData("topic", false)]
    [InlineData("topic", true)]
    public async Task InvokeDaprAsync_Poco_UsesSelectedJsonOptions(string trigger, bool customOptions)
    {
        var payload = new { EventId = "evt-42" };
        var options = customOptions ? new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower } : null;
        var result = trigger switch
        {
            "binding" => await TestHost.InvokeDaprBindingAsync("ProcessDaprBinding", payload, options, TestCancellation),
            "invocation" => await TestHost.InvokeDaprServiceInvocationAsync("ProcessDaprInvocation", payload, options, TestCancellation),
            _ => await TestHost.InvokeDaprTopicAsync("ProcessDaprTopic", payload, options, TestCancellation)
        };

        Assert.True(result.Success, $"Dapr JSON invocation failed: {result.Error}");
        Assert.Equal(customOptions ? """{"event_id":"evt-42"}""" : """{"eventId":"evt-42"}""",
            Assert.Single(_processedItems!.TakeAll()));
    }

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
