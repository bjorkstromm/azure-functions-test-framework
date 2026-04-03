using Azure.Messaging.ServiceBus;
using Azure.Storage.Queues.Models;
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Queue;
using AzureFunctions.TestFramework.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sample.FunctionApp.Worker;
using TUnit.Core;

namespace Sample.FunctionApp.Worker.TUnit.Tests;

/// <summary>
/// Integration tests for queue-triggered and Service Bus–triggered functions.
/// </summary>
public class TriggerFunctionsTests
{
    private IFunctionsTestHost? _testHost;
    private InMemoryProcessedItemsService? _processedItems;

    /// <summary>
    /// Starts a host with in-memory processed-items tracking before each test.
    /// </summary>
    [Before(Test)]
    public async Task SetUp()
    {
        // Arrange
        _processedItems = new InMemoryProcessedItemsService();

        var builder = new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new TUnitLoggerProvider())))
            .ConfigureServices(services =>
            {
                services.AddSingleton<ITodoService, InMemoryTodoService>();
                services.AddSingleton<IProcessedItemsService>(_processedItems);
            });

        _testHost = await builder.BuildAndStartAsync();
    }

    /// <summary>
    /// Stops the host after each test.
    /// </summary>
    [After(Test)]
    public async Task TearDown()
    {
        if (_testHost != null)
        {
            await _testHost.StopAsync();
            _testHost.Dispose();
        }
    }

    [Test]
    public async Task InvokeQueueAsync_WithTextMessage_Succeeds()
    {
        // Arrange
        var messageText = "Hello from queue!";
        var message = QueuesModelFactory.QueueMessage(
            messageId: Guid.NewGuid().ToString(),
            popReceipt: "pop-receipt",
            messageText: messageText,
            dequeueCount: 1);

        // Act
        var result = await _testHost!.InvokeQueueAsync("ProcessQueueMessage", message);

        // Assert
        TestContext.Current?.Output.WriteLine($"Success: {result.Success}, Error: {result.Error}");
        await Assert.That(result.Success).IsTrue();

        var processed = _processedItems!.TakeAll();
        await Assert.That(processed.Count).IsEqualTo(1);
        await Assert.That(processed[0]).IsEqualTo(messageText);
    }

    [Test]
    public async Task InvokeServiceBusAsync_WithTextBody_Succeeds()
    {
        // Arrange
        var body = "Hello from Service Bus!";
        var message = new ServiceBusMessage(body)
        {
            MessageId = Guid.NewGuid().ToString(),
            ContentType = "text/plain"
        };

        // Act
        var result = await _testHost!.InvokeServiceBusAsync("ProcessServiceBusMessage", message);

        // Assert
        TestContext.Current?.Output.WriteLine($"Success: {result.Success}, Error: {result.Error}");
        await Assert.That(result.Success).IsTrue();

        var processed = _processedItems!.TakeAll();
        await Assert.That(processed.Count).IsEqualTo(1);
        await Assert.That(processed[0]).IsEqualTo(body);
    }

    [Test]
    public async Task InvokeQueueAsync_CapturesPlainReturnValue()
    {
        // Arrange
        var messageText = "Hello return value!";
        var message = QueuesModelFactory.QueueMessage(
            messageId: Guid.NewGuid().ToString(),
            popReceipt: "pop-receipt",
            messageText: messageText,
            dequeueCount: 1);

        // Act
        var result = await _testHost!.InvokeQueueAsync("ReturnQueueMessageValue", message);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ReadReturnValueAs<string>()).IsEqualTo($"return:{messageText}");
        await Assert.That(result.OutputData.Count).IsEqualTo(0);
    }

    [Test]
    public async Task InvokeQueueAsync_CapturesOutputBindingData()
    {
        // Arrange
        var messageText = "Hello output binding!";
        var message = QueuesModelFactory.QueueMessage(
            messageId: Guid.NewGuid().ToString(),
            popReceipt: "pop-receipt",
            messageText: messageText,
            dequeueCount: 1);

        // Act
        var result = await _testHost!.InvokeQueueAsync("CreateQueueOutputMessages", message);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ReturnValue).IsNull();

        await Assert.That(result.OutputData.Count).IsEqualTo(1);
        var outputBinding = result.OutputData.Single();
        var messages = result.ReadOutputAs<string[]>(outputBinding.Key);

        await Assert.That(outputBinding.Key).IsEqualTo("Messages");
        await Assert.That(messages).IsNotNull();
        await Verify(messages!);
    }

    [Test]
    public async Task InvokeQueueAsync_CapturesBlobOutputBindingData()
    {
        // Arrange
        var messageText = "Hello blob binding!";
        var message = QueuesModelFactory.QueueMessage(
            messageId: Guid.NewGuid().ToString(),
            popReceipt: "pop-receipt",
            messageText: messageText,
            dequeueCount: 1);

        // Act
        var result = await _testHost!.InvokeQueueAsync("CreateBlobOutputDocument", message);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ReturnValue).IsNull();

        await Assert.That(result.OutputData.Count).IsEqualTo(1);
        var outputBinding = result.OutputData.Single();
        var content = result.ReadOutputAs<string>(outputBinding.Key);

        await Assert.That(outputBinding.Key).IsEqualTo("Content");
        await Assert.That(content).IsEqualTo($"blob:{messageText}");
    }

    [Test]
    public async Task InvokeQueueAsync_CapturesTableOutputBindingData()
    {
        // Arrange
        var messageText = "Hello table binding!";
        var message = QueuesModelFactory.QueueMessage(
            messageId: Guid.NewGuid().ToString(),
            popReceipt: "pop-receipt",
            messageText: messageText,
            dequeueCount: 1);

        // Act
        var result = await _testHost!.InvokeQueueAsync("CreateTableOutputEntity", message);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ReturnValue).IsNull();

        await Assert.That(result.OutputData.Count).IsEqualTo(1);
        var outputBinding = result.OutputData.Single();
        var entity = result.ReadOutputAs<CapturedTableEntity>(outputBinding.Key);

        await Assert.That(outputBinding.Key).IsEqualTo("Entity");
        await Assert.That(entity).IsNotNull();
        await Verify(entity!);
    }
}
