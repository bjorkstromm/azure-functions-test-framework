using Azure.Messaging.EventHubs;
using AzureFunctions.TestFramework.EventHubs;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventHubPayload = TestProject.EventHubTriggerFunction.EventHubPayload;
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

    [Fact]
    public async Task InvokeEventHubAsync_PreservesSdkEnvelope()
    {
        var message = new EventData(BinaryData.FromString("body"))
        {
            MessageId = "id",
            CorrelationId = "correlation"
        };
        message.Properties["custom"] = "value";
        var result = await TestHost.InvokeEventHubAsync("ProcessEventHubEnvelope", message, TestCancellation);
        Assert.True(result.Success, result.Error);
        Assert.Equal(["id|correlation|value|body"], _processedItems!.TakeAll());
    }

    [Fact]
    public async Task InvokeEventHubAsync_ConcreteAndExplicitGenericOverloads()
    {
        var raw = await TestHost.InvokeEventHubAsync("ProcessEventHubText", "plain", TestCancellation);
        var json = await TestHost.InvokeEventHubAsync<string>("ProcessEventHubText", "plain", TestCancellation);
        Assert.True(raw.Success, raw.Error);
        Assert.True(json.Success, json.Error);
        Assert.Equal(["plain", "\"plain\""], _processedItems!.TakeAll());
    }

    [Fact]
    public async Task InvokeEventHubAsync_BytesAndBinaryData_PreserveBinaryContent()
    {
        byte[] bytes = [0, 255, 128, 195, 40, 0];
        var raw = await TestHost.InvokeEventHubAsync("ProcessEventHubBytes", bytes, TestCancellation);
        var binary = await TestHost.InvokeEventHubAsync("ProcessEventHubBytes", new BinaryData(bytes), TestCancellation);
        Assert.True(raw.Success, raw.Error);
        Assert.True(binary.Success, binary.Error);
        Assert.Equal([Convert.ToBase64String(bytes), Convert.ToBase64String(bytes)], _processedItems!.TakeAll());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task InvokeEventHubBatchAsync_AllPayloadKinds_PreserveBatch(int count)
    {
        var text = Enumerable.Range(0, count).Select(i => $"event-{i}").ToArray();
        byte[][] bytes = Enumerable.Range(0, count).Select(i => new byte[] { 0, 255, 128, (byte)i }).ToArray();
        var sdk = await TestHost.InvokeEventHubBatchAsync("ProcessEventHubBatch",
            text.Select(message => new EventData(message)).ToArray(), TestCancellation);
        Assert.True(sdk.Success, sdk.Error);
        Assert.Equal(text, _processedItems!.TakeAll());

        var raw = await TestHost.InvokeEventHubBatchAsync("ProcessEventHubTextBatch", text, TestCancellation);
        Assert.True(raw.Success, raw.Error);
        Assert.Equal(text, _processedItems.TakeAll());

        var binary = await TestHost.InvokeEventHubBatchAsync("ProcessEventHubBytesBatch", bytes, TestCancellation);
        Assert.True(binary.Success, binary.Error);
        Assert.Equal(bytes.Select(Convert.ToBase64String), _processedItems.TakeAll());

        var data = await TestHost.InvokeEventHubBatchAsync("ProcessEventHubBytesBatch",
            bytes.Select(message => new BinaryData(message)).ToArray(), TestCancellation);
        Assert.True(data.Success, data.Error);
        Assert.Equal(bytes.Select(Convert.ToBase64String), _processedItems.TakeAll());

        var poco = await TestHost.InvokeEventHubBatchAsync("ProcessEventHubPayloadBatch",
            text.Select(message => new EventHubPayload { DisplayName = message }).ToArray(), TestCancellation);
        Assert.True(poco.Success, poco.Error);
        Assert.Equal(text, _processedItems.TakeAll());
    }

    [Fact]
    public async Task InvokeEventHubAsync_Poco_UsesCamelCaseByDefault()
    {
        var message = new EventHubPayload { DisplayName = "name" };
        var result = await TestHost.InvokeEventHubAsync("ProcessEventHubText", message, TestCancellation);
        Assert.True(result.Success, result.Error);
        Assert.Equal(["{\"displayName\":\"name\"}"], _processedItems!.TakeAll());
        var typed = await TestHost.InvokeEventHubAsync("ProcessEventHubPayload", message, TestCancellation);
        Assert.True(typed.Success, typed.Error);
        Assert.Equal(["name"], _processedItems.TakeAll());
    }

    [Fact]
    public async Task InvokeEventHubAsync_CustomSerializerOptions_ApplyToSingleAndBatch()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        options.Converters.Add(new JsonStringEnumConverter());
        var payload = new { DisplayName = "name", State = DayOfWeek.Monday };
        var single = await TestHost.InvokeEventHubAsync("ProcessEventHubText", payload, options, TestCancellation);
        Assert.True(single.Success, single.Error);
        Assert.Equal(["{\"display_name\":\"name\",\"state\":\"Monday\"}"], _processedItems!.TakeAll());
        var batch = await TestHost.InvokeEventHubBatchAsync("ProcessEventHubJsonBatch", new[] { payload }, options, TestCancellation);
        Assert.True(batch.Success, batch.Error);
        Assert.Equal(["{\"display_name\":\"name\",\"state\":\"Monday\"}"], _processedItems.TakeAll());
    }

    [Fact]
    public async Task InvokeEventHubAsync_RejectsNullPayloads()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => TestHost.InvokeEventHubAsync("unused", (string)null!, TestCancellation));
        await Assert.ThrowsAsync<ArgumentNullException>(() => TestHost.InvokeEventHubAsync("unused", (byte[])null!, TestCancellation));
        await Assert.ThrowsAsync<ArgumentNullException>(() => TestHost.InvokeEventHubAsync("unused", (BinaryData)null!, TestCancellation));
        await Assert.ThrowsAsync<ArgumentNullException>(() => TestHost.InvokeEventHubAsync("unused", (EventData)null!, TestCancellation));
        await Assert.ThrowsAsync<ArgumentNullException>(() => TestHost.InvokeEventHubAsync<EventHubPayload>("unused", null!, TestCancellation));
    }

    [Fact]
    public async Task InvokeEventHubAsync_EmptyBodies_AreValid()
    {
        var text = await TestHost.InvokeEventHubAsync("ProcessEventHubText", "", TestCancellation);
        var bytes = await TestHost.InvokeEventHubAsync("ProcessEventHubBytes", Array.Empty<byte>(), TestCancellation);
        Assert.True(text.Success, text.Error);
        Assert.True(bytes.Success, bytes.Error);
        Assert.Equal(["", ""], _processedItems!.TakeAll());
    }

    [Fact]
    public async Task InvokeEventHubBatchAsync_RejectsNullEmptyAndNullElements()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => TestHost.InvokeEventHubBatchAsync("unused", (string[])null!, TestCancellation));
        await Assert.ThrowsAsync<ArgumentNullException>(() => TestHost.InvokeEventHubBatchAsync("unused", (byte[][])null!, TestCancellation));
        await Assert.ThrowsAsync<ArgumentNullException>(() => TestHost.InvokeEventHubBatchAsync("unused", (BinaryData[])null!, TestCancellation));
        await Assert.ThrowsAsync<ArgumentNullException>(() => TestHost.InvokeEventHubBatchAsync("unused", (EventData[])null!, TestCancellation));
        await Assert.ThrowsAsync<ArgumentNullException>(() => TestHost.InvokeEventHubBatchAsync<EventHubPayload>("unused", null!, TestCancellation));
        await Assert.ThrowsAsync<ArgumentException>(() => TestHost.InvokeEventHubBatchAsync("unused", Array.Empty<string>(), TestCancellation));
        await Assert.ThrowsAsync<ArgumentException>(() => TestHost.InvokeEventHubBatchAsync("unused", Array.Empty<byte[]>(), TestCancellation));
        await Assert.ThrowsAsync<ArgumentException>(() => TestHost.InvokeEventHubBatchAsync("unused", Array.Empty<BinaryData>(), TestCancellation));
        await Assert.ThrowsAsync<ArgumentException>(() => TestHost.InvokeEventHubBatchAsync("unused", Array.Empty<EventData>(), TestCancellation));
        await Assert.ThrowsAsync<ArgumentException>(() => TestHost.InvokeEventHubBatchAsync("unused", Array.Empty<EventHubPayload>(), TestCancellation));
        await Assert.ThrowsAsync<ArgumentException>(() => TestHost.InvokeEventHubBatchAsync("unused", new string[] { null! }, TestCancellation));
        await Assert.ThrowsAsync<ArgumentException>(() => TestHost.InvokeEventHubBatchAsync("unused", new byte[][] { null! }, TestCancellation));
        await Assert.ThrowsAsync<ArgumentException>(() => TestHost.InvokeEventHubBatchAsync("unused", new BinaryData[] { null! }, TestCancellation));
        await Assert.ThrowsAsync<ArgumentException>(() => TestHost.InvokeEventHubBatchAsync("unused", new EventData[] { null! }, TestCancellation));
        await Assert.ThrowsAsync<ArgumentException>(() => TestHost.InvokeEventHubBatchAsync("unused", new EventHubPayload[] { null! }, TestCancellation));
    }
}
