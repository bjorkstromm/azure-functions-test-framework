using Azure.Messaging.ServiceBus;
using Azure.Storage.Queues.Models;
using AzureFunctions.TestFramework.Queue;
using AzureFunctions.TestFramework.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

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
}
