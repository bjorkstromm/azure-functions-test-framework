using System.Text;
using Azure.Storage.Queues.Models;
using Azure.Messaging.ServiceBus;
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Queue;
using AzureFunctions.TestFramework.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Sample.FunctionApp.Worker;
using Xunit;
using Xunit.Abstractions;

namespace Sample.FunctionApp.Worker.Tests;

/// <summary>
/// Integration tests for queue-triggered and Service Bus–triggered functions.
/// </summary>
public class TriggerFunctionsTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IFunctionsTestHost? _testHost;
    private InMemoryProcessedItemsService? _processedItems;

    public TriggerFunctionsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _processedItems = new InMemoryProcessedItemsService();

        var builder = new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .ConfigureServices(services =>
            {
                services.AddSingleton<ITodoService, InMemoryTodoService>();
                services.AddSingleton<IProcessedItemsService>(_processedItems);
            });

        _testHost = await builder.BuildAndStartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_testHost != null)
        {
            await _testHost.StopAsync();
            _testHost.Dispose();
        }
    }

    [Fact]
    public async Task InvokeQueueAsync_WithTextMessage_Succeeds()
    {
        var messageText = "Hello from queue!";
        var message = QueuesModelFactory.QueueMessage(
            messageId: Guid.NewGuid().ToString(),
            popReceipt: "pop-receipt",
            messageText: messageText,
            dequeueCount: 1);

        var result = await _testHost!.InvokeQueueAsync("ProcessQueueMessage", message);

        _output.WriteLine($"Success: {result.Success}, Error: {result.Error}");
        Assert.True(result.Success, $"Queue invocation failed: {result.Error}");

        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(messageText, processed[0]);
    }

    [Fact]
    public async Task InvokeServiceBusAsync_WithTextBody_Succeeds()
    {
        var body = "Hello from Service Bus!";
        var message = new ServiceBusMessage(body)
        {
            MessageId = Guid.NewGuid().ToString(),
            ContentType = "text/plain"
        };

        var result = await _testHost!.InvokeServiceBusAsync("ProcessServiceBusMessage", message);

        _output.WriteLine($"Success: {result.Success}, Error: {result.Error}");
        Assert.True(result.Success, $"Service Bus invocation failed: {result.Error}");

        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(body, processed[0]);
    }

    [Fact]
    public async Task InvokeQueueAsync_CapturesPlainReturnValue()
    {
        var messageText = "Hello return value!";
        var message = QueuesModelFactory.QueueMessage(
            messageId: Guid.NewGuid().ToString(),
            popReceipt: "pop-receipt",
            messageText: messageText,
            dequeueCount: 1);

        var result = await _testHost!.InvokeQueueAsync("ReturnQueueMessageValue", message);

        Assert.True(result.Success, $"Queue invocation failed: {result.Error}");
        Assert.Equal($"return:{messageText}", result.ReadReturnValueAs<string>());
        Assert.Empty(result.OutputData);
    }

    [Fact]
    public async Task InvokeQueueAsync_CapturesOutputBindingData()
    {
        var messageText = "Hello output binding!";
        var message = QueuesModelFactory.QueueMessage(
            messageId: Guid.NewGuid().ToString(),
            popReceipt: "pop-receipt",
            messageText: messageText,
            dequeueCount: 1);

        var result = await _testHost!.InvokeQueueAsync("CreateQueueOutputMessages", message);

        Assert.True(result.Success, $"Queue invocation failed: {result.Error}");
        Assert.Null(result.ReturnValue);

        var outputBinding = Assert.Single(result.OutputData);
        var messages = result.ReadOutputAs<string[]>(outputBinding.Key);

        Assert.Equal("Messages", outputBinding.Key);
        Assert.NotNull(messages);
        Assert.Equal(
            [$"output:{messageText}", $"output:{messageText}:copy"],
            messages!);
    }

    [Fact]
    public async Task InvokeQueueAsync_CapturesBlobOutputBindingData()
    {
        var messageText = "Hello blob binding!";
        var message = QueuesModelFactory.QueueMessage(
            messageId: Guid.NewGuid().ToString(),
            popReceipt: "pop-receipt",
            messageText: messageText,
            dequeueCount: 1);

        var result = await _testHost!.InvokeQueueAsync("CreateBlobOutputDocument", message);

        Assert.True(result.Success, $"Queue invocation failed: {result.Error}");
        Assert.Null(result.ReturnValue);

        var outputBinding = Assert.Single(result.OutputData);
        var content = result.ReadOutputAs<string>(outputBinding.Key);

        Assert.Equal("Content", outputBinding.Key);
        Assert.Equal($"blob:{messageText}", content);
    }

    [Fact]
    public async Task InvokeQueueAsync_CapturesTableOutputBindingData()
    {
        var messageText = "Hello table binding!";
        var message = QueuesModelFactory.QueueMessage(
            messageId: Guid.NewGuid().ToString(),
            popReceipt: "pop-receipt",
            messageText: messageText,
            dequeueCount: 1);

        var result = await _testHost!.InvokeQueueAsync("CreateTableOutputEntity", message);

        Assert.True(result.Success, $"Queue invocation failed: {result.Error}");
        Assert.Null(result.ReturnValue);

        var outputBinding = Assert.Single(result.OutputData);
        var entity = result.ReadOutputAs<CapturedTableEntity>(outputBinding.Key);

        Assert.Equal("Entity", outputBinding.Key);
        Assert.NotNull(entity);
        Assert.Equal("captured", entity!.PartitionKey);
        Assert.Equal($"row-{messageText.Length}", entity.RowKey);
        Assert.Equal($"table:{messageText}", entity.Payload);
    }
}
