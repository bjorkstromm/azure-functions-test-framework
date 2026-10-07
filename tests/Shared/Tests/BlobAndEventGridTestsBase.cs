using Azure.Messaging.EventGrid;
using AzureFunctions.TestFramework.Blob;
using AzureFunctions.TestFramework.EventGrid;
using AzureFunctions.TestFramework.Queue;
using Xunit;

namespace TestProject;

/// <summary>Tests for blob-triggered, blob-input, and Event Grid–triggered functions.</summary>
public abstract class BlobAndEventGridTestsBase : TestHostTestBase
{
    /// <summary>
    /// The <c>[BlobInput]</c> blob path registered by concrete test classes in the host builder.
    /// Must match the <c>blobPath</c> argument of <c>[BlobInput]</c> on <see cref="BlobInputFunction"/>.
    /// </summary>
    protected const string BlobInputTestPath = "test-input/data.txt";

    /// <summary>Expected content injected for the <see cref="BlobInputTestPath"/> binding.</summary>
    protected const string BlobInputTestContent = "hello from blob input!";

    /// <summary>
    /// The <c>[BlobInput]</c> blob path for BlobClient-typed input bindings.
    /// Must match the <c>blobPath</c> argument of <c>[BlobInput]</c> on <c>ReadBlobInputClient</c>.
    /// </summary>
    protected const string BlobInputClientTestPath = "test-input-client/data.txt";

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
    public async Task InvokeBlobContentAsync_WithText_PreservesNameAndContent()
    {
        var result = await TestHost.InvokeBlobContentAsync(
            "ProcessBlob", "hello \u2603", "text.txt", cancellationToken: TestCancellation);

        Assert.True(result.Success, result.Error);
        Assert.Equal("text.txt:hello \u2603", Assert.Single(_processedItems!.TakeAll()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvokeBlobAsync_WithRawContent_PreservesBinaryBytes(bool useBinaryData)
    {
        byte[] bytes = [0, 255, 128, 195, 40];
        var result = useBinaryData
            ? await TestHost.InvokeBlobAsync("ProcessBlobBytes", new BinaryData(bytes), "data.bin", cancellationToken: TestCancellation)
            : await TestHost.InvokeBlobAsync("ProcessBlobBytes", bytes, "data.bin", cancellationToken: TestCancellation);

        Assert.True(result.Success, result.Error);
        Assert.Equal($"data.bin:{Convert.ToBase64String(bytes)}", Assert.Single(_processedItems!.TakeAll()));
    }

    [Fact]
    public async Task InvokeBlobAsync_WithStream_ReadsRemainingBytesAndLeavesStreamOpen()
    {
        byte[] bytes = [42, 0, 255, 128];
        using var stream = new MemoryStream(bytes);
        stream.Position = 1;
        var result = await TestHost.InvokeBlobAsync("ProcessBlobBytes", stream, "data.bin", cancellationToken: TestCancellation);

        Assert.True(result.Success, result.Error);
        Assert.Equal($"data.bin:{Convert.ToBase64String(bytes[1..])}", Assert.Single(_processedItems!.TakeAll()));
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task InvokeBlobAsync_WithCancelledStream_DoesNotInvokeOrDisposeStream()
    {
        using var stream = new MemoryStream([0, 255]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            TestHost.InvokeBlobAsync("ProcessBlobBytes", stream, cancellationToken: cancellation.Token));

        Assert.Empty(_processedItems!.TakeAll());
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task InvokeBlobAsync_RejectsNullContent()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            TestHost.InvokeBlobAsync("ProcessBlobBytes", (byte[])null!, cancellationToken: TestCancellation));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            TestHost.InvokeBlobAsync("ProcessBlobBytes", (BinaryData)null!, cancellationToken: TestCancellation));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            TestHost.InvokeBlobContentAsync("ProcessBlob", null!, cancellationToken: TestCancellation));
    }

    [Fact]
    public async Task InvokeBlobAsync_WithBlobClientParam_Succeeds()
    {
        var result = await TestHost.InvokeBlobAsync(
            "ProcessBlobClient",
            "test-container",
            "myblob.txt",
            cancellationToken: TestCancellation);

        Assert.True(result.Success, $"BlobClient trigger invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal("test-container/myblob.txt", processed[0]);
    }

    [Fact]
    public async Task InvokeWithBlobInput_ReadsRegisteredContent()
    {
        // The concrete test class registers BlobInputTestPath → BlobInputTestContent
        // in the host builder via WithBlobInputContent(). The ReadBlobInput function reads
        // that content via [BlobInput("test-input/data.txt")] and stores it.
        var result = await TestHost.InvokeQueueAsync("ReadBlobInput", "unused", TestCancellation);

        Assert.True(result.Success, $"BlobInput invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(BlobInputTestContent, processed[0]);
    }

    [Fact]
    public async Task InvokeBlobInput_WithBlobClientParam_Succeeds()
    {
        var result = await TestHost.InvokeQueueAsync("ReadBlobInputClient", "unused", TestCancellation);

        Assert.True(result.Success, $"BlobClient input invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal("test-input-client/data.txt", processed[0]);
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
