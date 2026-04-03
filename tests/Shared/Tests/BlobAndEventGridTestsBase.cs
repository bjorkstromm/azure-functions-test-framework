using Azure.Messaging.EventGrid;
using AzureFunctions.TestFramework.Blob;
using AzureFunctions.TestFramework.EventGrid;
using Xunit.Abstractions;

namespace TestProject;

/// <summary>Tests for blob-triggered and Event Grid–triggered functions.</summary>
public abstract class BlobAndEventGridTestsBase : TestHostTestBase
{
    private InMemoryProcessedItemsService? _processedItems;

    protected BlobAndEventGridTestsBase(ITestOutputHelper output) : base(output) { }

    protected override async Task<IFunctionsTestHost> CreateTestHostAsync()
    {
        _processedItems = new InMemoryProcessedItemsService();
        return await CreateTestHostWithProcessedItemsAsync(_processedItems);
    }

    protected abstract Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(InMemoryProcessedItemsService processedItems);

    [Fact]
    public async Task InvokeBlobAsync_WithTextContent_Succeeds()
    {
        var content = BinaryData.FromString("Hello from blob!");
        var result = await TestHost.InvokeBlobAsync("ProcessBlob", content, "file.txt", cancellationToken: TestCancellation);

        Assert.True(result.Success, $"Blob invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Contains("Hello from blob!", processed[0]);
    }

    [Fact]
    public async Task InvokeEventGridAsync_WithEventGridEvent_Succeeds()
    {
        var subject = "test/subject";
        var evt = new EventGridEvent(subject, "Test.Event", "1.0",
            BinaryData.FromObjectAsJson(new { message = "Hello from Event Grid!" }));

        var result = await TestHost.InvokeEventGridAsync("ProcessEventGridEvent", evt, TestCancellation);

        Assert.True(result.Success, $"Event Grid invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(subject, processed[0]);
    }
}
