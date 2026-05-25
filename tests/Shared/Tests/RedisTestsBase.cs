using AzureFunctions.TestFramework.Queue;
using AzureFunctions.TestFramework.Redis;
using Xunit;

namespace TestProject;

/// <summary>
/// Tests covering Redis pub/sub trigger, list trigger, stream trigger, input, and output bindings.
/// </summary>
public abstract class RedisTestsBase : TestHostTestBase
{
    private InMemoryProcessedItemsService? _processedItems;

    protected RedisTestsBase(ITestOutputHelper output) : base(output) { }

    protected override async Task<IFunctionsTestHost> CreateTestHostAsync()
    {
        _processedItems = new InMemoryProcessedItemsService();
        return await CreateTestHostWithProcessedItemsAsync(_processedItems);
    }

    /// <summary>
    /// Creates the test host with the given <see cref="InMemoryProcessedItemsService"/>.
    /// Concrete implementations must register the processed items service and call
    /// <c>.WithRedisInput(<see cref="RedisFunction.InputCommand"/>, <see cref="RedisInputTestValue"/>)</c>
    /// on the builder so that the <see cref="InvokeQueueAsync_WithRedisInput_ReadsRegisteredValue"/> test
    /// receives the expected value.
    /// </summary>
    protected abstract Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(
        InMemoryProcessedItemsService processedItems);

    // -------------------------------------------------------------------------
    // Pub/Sub trigger tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that a <c>[RedisPubSubTrigger]</c> function receives the pub/sub message string.
    /// </summary>
    [Fact]
    public async Task InvokeRedisPubSubAsync_Succeeds()
    {
        const string message = "hello from redis pub/sub";

        var result = await TestHost.InvokeRedisPubSubAsync(
            "ProcessRedisPubSub",
            RedisFunction.PubSubChannel,
            message,
            TestCancellation);

        Assert.True(result.Success, $"Pub/sub invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal($"pubsub:{message}", processed[0]);
    }

    // -------------------------------------------------------------------------
    // List trigger tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that a <c>[RedisListTrigger]</c> function receives the list entry string.
    /// </summary>
    [Fact]
    public async Task InvokeRedisListAsync_Succeeds()
    {
        const string value = "task-payload";

        var result = await TestHost.InvokeRedisListAsync(
            "ProcessRedisList",
            RedisFunction.ListKey,
            value,
            TestCancellation);

        Assert.True(result.Success, $"List trigger invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal($"list:{value}", processed[0]);
    }

    // -------------------------------------------------------------------------
    // Stream trigger tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that a <c>[RedisStreamTrigger]</c> function receives the stream entries as JSON.
    /// </summary>
    [Fact]
    public async Task InvokeRedisStreamAsync_Succeeds()
    {
        var entries = new[]
        {
            new KeyValuePair<string, string>("field1", "value1"),
            new KeyValuePair<string, string>("field2", "value2")
        };

        var result = await TestHost.InvokeRedisStreamAsync(
            "ProcessRedisStream",
            RedisFunction.StreamKey,
            entries,
            TestCancellation);

        Assert.True(result.Success, $"Stream trigger invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.StartsWith("stream:", processed[0]);
        Assert.Contains("field1", processed[0]);
        Assert.Contains("value1", processed[0]);
    }

    // -------------------------------------------------------------------------
    // Output binding test
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that a <c>[RedisPubSubTrigger]</c> function with a <c>[RedisOutput]</c>
    /// return value captures the echoed message.
    /// </summary>
    [Fact]
    public async Task InvokeRedisPubSubAsync_WithOutputBinding_CapturesReturnValue()
    {
        const string message = "output-test-message";

        var result = await TestHost.InvokeRedisPubSubAsync(
            "EchoRedisPubSubWithOutput",
            RedisFunction.PubSubChannel,
            message,
            TestCancellation);

        Assert.True(result.Success, $"Output binding invocation failed: {result.Error}");

        // Verify trigger side-effect
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(message, processed[0]);

        // Verify captured return value
        var written = result.ReadReturnValueAs<string>();
        Assert.Equal(message, written);
    }

    // -------------------------------------------------------------------------
    // Input binding test
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that a queue-triggered function with a <c>[RedisInput]</c> binding receives
    /// the value registered via <c>WithRedisInput</c>.
    /// </summary>
    [Fact]
    public async Task InvokeQueueAsync_WithRedisInput_ReadsRegisteredValue()
    {
        var result = await TestHost.InvokeQueueAsync("ReadRedisInput", "unused", TestCancellation);

        Assert.True(result.Success, $"Redis input binding invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(RedisInputTestValue, processed[0]);
    }

    /// <summary>
    /// The fake Redis value injected for the <c>[RedisInput]</c> test.
    /// Must match the value registered in <see cref="CreateTestHostWithProcessedItemsAsync"/>.
    /// </summary>
    protected const string RedisInputTestValue = "hello from redis input!";
}
