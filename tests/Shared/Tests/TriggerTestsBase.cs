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
        var message = QueuesModelFactory.QueueMessage(Guid.NewGuid().ToString(), "pop-receipt", messageText, 1);

        var result = await TestHost.InvokeQueueAsync("ProcessQueueMessage", message, TestCancellation);

        Assert.True(result.Success, $"Queue invocation failed: {result.Error}");
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
        var message = QueuesModelFactory.QueueMessage(Guid.NewGuid().ToString(), "pop-receipt", messageText, 1);

        var result = await TestHost.InvokeQueueAsync("ReturnQueueMessageValue", message, TestCancellation);

        Assert.True(result.Success, $"Queue invocation failed: {result.Error}");
        Assert.Equal($"return:{messageText}", result.ReadReturnValueAs<string>());
    }

    [Fact]
    public async Task InvokeQueueAsync_CapturesOutputBindingData()
    {
        var messageText = "Hello output binding!";
        var message = QueuesModelFactory.QueueMessage(Guid.NewGuid().ToString(), "pop-receipt", messageText, 1);

        var result = await TestHost.InvokeQueueAsync("CreateQueueOutputMessages", message, TestCancellation);

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
        var message = QueuesModelFactory.QueueMessage(Guid.NewGuid().ToString(), "pop-receipt", messageText, 1);

        var result = await TestHost.InvokeQueueAsync("CreateBlobOutputDocument", message, TestCancellation);

        Assert.True(result.Success, $"Queue invocation failed: {result.Error}");
        var content = result.ReadOutputAs<string>("Content");
        Assert.Equal($"blob:{messageText}", content);
    }

    [Fact]
    public async Task InvokeQueueAsync_CapturesTableOutputBindingData()
    {
        var messageText = "Hello table output!";
        var message = QueuesModelFactory.QueueMessage(Guid.NewGuid().ToString(), "pop-receipt", messageText, 1);

        var result = await TestHost.InvokeQueueAsync("CreateTableOutputEntity", message, TestCancellation);

        Assert.True(result.Success, $"Queue invocation failed: {result.Error}");
        var entity = result.ReadOutputAs<CapturedTableEntity>("Entity");
        Assert.NotNull(entity);
        Assert.Equal("captured", entity.PartitionKey);
        Assert.Equal($"table:{messageText}", entity.Payload);
    }
}
