using Azure.Messaging.EventHubs;
using AzureFunctions.TestFramework.EventHubs;
using Xunit;

namespace TestProject;

/// <summary>Tests for Event Hubs–triggered functions and output bindings.</summary>
public abstract class EventHubsTestsBase : TestHostTestBase
{
    private InMemoryProcessedItemsService? _processedItems;

    protected EventHubsTestsBase(ITestOutputHelper output) : base(output) { }

    protected override async Task<IFunctionsTestHost> CreateTestHostAsync()
    {
        _processedItems = new InMemoryProcessedItemsService();
        return await CreateTestHostWithProcessedItemsAsync(_processedItems);
    }

    protected abstract Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(InMemoryProcessedItemsService processedItems);

    [Fact]
    public async Task InvokeEventHubAsync_WithSingleEvent_Succeeds()
    {
        var body = "Hello from Event Hubs!";
        var eventData = new EventData(BinaryData.FromString(body));

        var result = await TestHost.InvokeEventHubAsync("ProcessEventHubMessage", eventData, TestCancellation);

        Assert.True(result.Success, $"Event Hubs single event invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(body, processed[0]);
    }

    [Fact]
    public async Task InvokeEventHubBatchAsync_WithMultipleEvents_Succeeds()
    {
        var bodies = new[] { "Batch event 1", "Batch event 2", "Batch event 3" };
        var events = bodies.Select(b => new EventData(BinaryData.FromString(b))).ToArray();

        var result = await TestHost.InvokeEventHubBatchAsync("ProcessEventHubBatch", events, TestCancellation);

        Assert.True(result.Success, $"Event Hubs batch invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Equal(3, processed.Count);
        Assert.Equal(bodies, processed);
    }

    [Fact]
    public async Task InvokeEventHubAsync_WithOutputBinding_CapturesReturnValue()
    {
        var body = "trigger-payload";
        var eventData = new EventData(BinaryData.FromString(body));

        var result = await TestHost.InvokeEventHubAsync("ForwardEventHubMessage", eventData, TestCancellation);

        Assert.True(result.Success, $"Event Hubs output binding invocation failed: {result.Error}");

        // Verify trigger side-effect
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(body, processed[0]);

        // Verify the return value (EventHubOutput uses the function return value)
        var forwarded = result.ReadReturnValueAs<string>();
        Assert.NotNull(forwarded);
        Assert.Equal($"forwarded:{body}", forwarded);
    }
}
