using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Queues.Models;
using AzureFunctions.TestFramework.Queue;
using AzureFunctions.TestFramework.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TestProject;

/// <summary>Tests for queue-triggered and Service Bus–triggered functions.</summary>
public abstract class TriggerTestsBase : TestHostTestBase
{
    private InMemoryProcessedItemsService? _processedItems;

    protected TriggerTestsBase(ITestOutputHelper output) : base(output) { }

    protected override async Task<IFunctionsTestHost> CreateTestHostAsync()
    {
        _processedItems = new InMemoryProcessedItemsService();
        return await CreateTestHostWithProcessedItemsAsync(_processedItems);
    }

    protected abstract Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(InMemoryProcessedItemsService processedItems);

    [Fact]
    public async Task InvokeQueueAsync_WithTextMessage_Succeeds()
    {
        var messageText = "Hello from queue!";

        var result = await TestHost.InvokeQueueAsync("ProcessQueueMessage", messageText, TestCancellation);

        Assert.True(result.Success, $"Queue invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(messageText, processed[0]);
    }

    [Fact]
    public async Task InvokeQueueAsync_WithQueueMessageParam_Succeeds()
    {
        var messageText = "Hello typed queue!";
        var message = QueuesModelFactory.QueueMessage(Guid.NewGuid().ToString(), "pop-receipt", messageText, 1);

        var result = await TestHost.InvokeQueueAsync("ProcessQueueMessageTyped", message, TestCancellation);

        Assert.True(result.Success, $"Typed QueueMessage invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(messageText, processed[0]);
    }

    [Fact]
    public async Task InvokeServiceBusAsync_WithTextBody_Succeeds()
    {
        var body = "Hello from Service Bus!";
        var message = new ServiceBusMessage(body) { MessageId = Guid.NewGuid().ToString() };

        var result = await TestHost.InvokeServiceBusAsync("ProcessServiceBusMessage", message, TestCancellation);

        Assert.True(result.Success, $"Service Bus invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(body, processed[0]);
    }

    [Fact]
    public async Task InvokeServiceBusAsync_WithString_PreservesText()
    {
        const string body = "Hello \"Service Bus\"! café 🚀";
        var result = await TestHost.InvokeServiceBusAsync("ProcessServiceBusMessage", body, TestCancellation);

        Assert.True(result.Success, result.Error);
        Assert.Equal(body, Assert.Single(_processedItems!.TakeAll()));
    }

    [Fact]
    public async Task InvokeServiceBusAsync_WithBytes_PreservesNonUtf8Body()
    {
        byte[] body = [0, 0xff, 0xfe, 0x80, 0xc3, 0x28];
        var result = await TestHost.InvokeServiceBusAsync("ProcessServiceBusBytes", body, TestCancellation);

        Assert.True(result.Success, result.Error);
        Assert.Equal(Convert.ToBase64String(body), Assert.Single(_processedItems!.TakeAll()));
    }

    [Fact]
    public async Task InvokeServiceBusAsync_WithBinaryData_PreservesNonUtf8Body()
    {
        byte[] body = [0, 0xff, 0xfe, 0x80, 0xc3, 0x28];
        var result = await TestHost.InvokeServiceBusAsync(
            "ProcessServiceBusBytes", BinaryData.FromBytes(body), TestCancellation);

        Assert.True(result.Success, result.Error);
        Assert.Equal(Convert.ToBase64String(body), Assert.Single(_processedItems!.TakeAll()));
    }

    [Fact]
    public async Task InvokeServiceBusAsync_WithPoco_BindsPayload()
    {
        var result = await TestHost.InvokeServiceBusAsync(
            "ProcessServiceBusPayload", new ServiceBusPayload { OrderName = "order", Quantity = 3 },
            cancellationToken: TestCancellation);

        Assert.True(result.Success, result.Error);
        Assert.Equal("order:3", Assert.Single(_processedItems!.TakeAll()));
    }

    [Fact]
    public async Task InvokeServiceBusAsync_WithPoco_DefaultsToCamelCase()
    {
        var result = await TestHost.InvokeServiceBusAsync(
            "ProcessServiceBusMessage", new ServiceBusPayload { OrderName = "order", Quantity = 3 },
            cancellationToken: TestCancellation);

        Assert.True(result.Success, result.Error);
        using var json = JsonDocument.Parse(Assert.Single(_processedItems!.TakeAll()));
        Assert.Equal("order", json.RootElement.GetProperty("orderName").GetString());
        Assert.Equal(3, json.RootElement.GetProperty("quantity").GetInt32());
        Assert.False(json.RootElement.TryGetProperty("OrderName", out _));
    }

    [Fact]
    public async Task InvokeServiceBusAsync_WithPoco_UsesCustomOptions()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        var result = await TestHost.InvokeServiceBusAsync(
            "ProcessServiceBusMessage", new ServiceBusPayload { OrderName = "order", Quantity = 3 },
            options, TestCancellation);

        Assert.True(result.Success, result.Error);
        using var json = JsonDocument.Parse(Assert.Single(_processedItems!.TakeAll()));
        Assert.Equal("order", json.RootElement.GetProperty("order_name").GetString());
        Assert.Equal(3, json.RootElement.GetProperty("quantity").GetInt32());
        Assert.Same(JsonNamingPolicy.SnakeCaseLower, options.PropertyNamingPolicy);
    }

    [Fact]
    public async Task InvokeServiceBusAsync_WithSdkMessage_PreservesRawBytes()
    {
        byte[] body = [0, 0xff, 0xfe, 0x80];
        var message = new ServiceBusMessage(BinaryData.FromBytes(body)) { MessageId = "sdk-message" };
        var result = await TestHost.InvokeServiceBusAsync("ProcessServiceBusBytes", message, TestCancellation);

        Assert.True(result.Success, result.Error);
        Assert.Equal(Convert.ToBase64String(body), Assert.Single(_processedItems!.TakeAll()));
    }

    [Fact]
    public async Task InvokeServiceBusAsync_WithReceivedMessage_PreservesMetadata()
    {
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("body"), messageId: "message-id", correlationId: "correlation-id",
            subject: "subject", contentType: "application/json", sessionId: "session-id",
            properties: new Dictionary<string, object> { ["source"] = "test" });
        var result = await TestHost.InvokeServiceBusAsync("ProcessServiceBusReceivedMessageMetadata", message, TestCancellation);

        Assert.True(result.Success, result.Error);
        Assert.Equal("message-id|correlation-id|subject|application/json|session-id|test",
            Assert.Single(_processedItems!.TakeAll()));
    }

    [Fact]
    public async Task InvokeServiceBusBatchAsync_WithSingleMessage_RemainsBatch()
    {
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("only message"));
        var result = await TestHost.InvokeServiceBusBatchAsync("ProcessServiceBusMessageBatch", [message], TestCancellation);

        Assert.True(result.Success, result.Error);
        Assert.Equal("only message", Assert.Single(_processedItems!.TakeAll()));
    }

    [Fact]
    public async Task InvokeServiceBusAsync_WithNullPayload_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            TestHost.InvokeServiceBusAsync("ProcessServiceBusMessage", (string)null!, TestCancellation));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            TestHost.InvokeServiceBusAsync("ProcessServiceBusBytes", (byte[])null!, TestCancellation));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            TestHost.InvokeServiceBusAsync("ProcessServiceBusBytes", (BinaryData)null!, TestCancellation));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            TestHost.InvokeServiceBusAsync<ServiceBusPayload>("ProcessServiceBusPayload", null!, TestCancellation));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            TestHost.InvokeServiceBusAsync("ProcessServiceBusMessage", (ServiceBusMessage)null!, TestCancellation));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            TestHost.InvokeServiceBusAsync("ProcessServiceBusReceivedMessage", (ServiceBusReceivedMessage)null!, TestCancellation));
    }

    [Fact]
    public async Task InvokeServiceBusBatchAsync_WithInvalidBatch_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            TestHost.InvokeServiceBusBatchAsync("ProcessServiceBusMessageBatch", null!, TestCancellation));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            TestHost.InvokeServiceBusBatchAsync("ProcessServiceBusMessageBatch", [], TestCancellation));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            TestHost.InvokeServiceBusBatchAsync("ProcessServiceBusMessageBatch", [null!], TestCancellation));
    }

    [Fact]
    public async Task InvokeServiceBusAsync_WithReceivedMessage_Succeeds()
    {
        var body = "Hello as ServiceBusReceivedMessage!";
        var receivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(body),
            messageId: Guid.NewGuid().ToString());

        var result = await TestHost.InvokeServiceBusAsync("ProcessServiceBusReceivedMessage", receivedMessage, TestCancellation);

        Assert.True(result.Success, $"Service Bus ReceivedMessage invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(body, processed[0]);
    }

    [Fact]
    public async Task InvokeServiceBusBatchAsync_WithMultipleMessages_Succeeds()
    {
        var bodies = new[] { "Batch message 1", "Batch message 2", "Batch message 3" };
        var messages = bodies
            .Select(b => ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString(b),
                messageId: Guid.NewGuid().ToString()))
            .ToList()
            .AsReadOnly();

        var result = await TestHost.InvokeServiceBusBatchAsync("ProcessServiceBusMessageBatch", messages, TestCancellation);

        Assert.True(result.Success, $"Service Bus batch invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Equal(3, processed.Count);
        Assert.Equal(bodies, processed);
    }

    [Fact]
    public async Task InvokeQueueAsync_CapturesPlainReturnValue()
    {
        var messageText = "Hello return value!";

        var result = await TestHost.InvokeQueueAsync("ReturnQueueMessageValue", messageText, TestCancellation);

        Assert.True(result.Success, $"Queue invocation failed: {result.Error}");
        Assert.Equal($"return:{messageText}", result.ReadReturnValueAs<string>());
    }

    [Fact]
    public async Task InvokeQueueAsync_CapturesOutputBindingData()
    {
        var messageText = "Hello output binding!";

        var result = await TestHost.InvokeQueueAsync("CreateQueueOutputMessages", messageText, TestCancellation);

        Assert.True(result.Success, $"Queue invocation failed: {result.Error}");
        var outputBinding = Assert.Single(result.OutputData);
        var messages = result.ReadOutputAs<string[]>(outputBinding.Key);
        Assert.NotNull(messages);
        Assert.Equal(2, messages.Length);
    }

    [Fact]
    public async Task InvokeQueueAsync_CapturesBlobOutputBindingData()
    {
        var messageText = "Hello blob output!";

        var result = await TestHost.InvokeQueueAsync("CreateBlobOutputDocument", messageText, TestCancellation);

        Assert.True(result.Success, $"Queue invocation failed: {result.Error}");
        var content = result.ReadOutputAs<string>("Content");
        Assert.Equal($"blob:{messageText}", content);
    }

    [Fact]
    public async Task InvokeQueueAsync_CapturesTableOutputBindingData()
    {
        var messageText = "Hello table output!";

        var result = await TestHost.InvokeQueueAsync("CreateTableOutputEntity", messageText, TestCancellation);

        Assert.True(result.Success, $"Queue invocation failed: {result.Error}");
        var entity = result.ReadOutputAs<CapturedTableEntity>("Entity");
        Assert.NotNull(entity);
        Assert.Equal("captured", entity.PartitionKey);
        Assert.Equal($"table:{messageText}", entity.Payload);
    }
}
