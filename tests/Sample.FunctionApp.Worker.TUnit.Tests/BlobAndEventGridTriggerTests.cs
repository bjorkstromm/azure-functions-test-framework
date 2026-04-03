using Azure.Messaging.EventGrid;
using AzureFunctions.TestFramework.Blob;
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.EventGrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sample.FunctionApp.Worker;
using TUnit.Core;

namespace Sample.FunctionApp.Worker.TUnit.Tests;

/// <summary>
/// Integration tests for blob-triggered and Event Grid–triggered functions.
/// </summary>
public class BlobAndEventGridTriggerTests
{
    private IFunctionsTestHost? _testHost;
    private InMemoryProcessedItemsService? _processedItems;

    /// <summary>
    /// Starts a host with supporting services before each test.
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
    public async Task InvokeBlobAsync_WithTextContent_Succeeds()
    {
        // Arrange
        var content = BinaryData.FromString("Hello from blob!");
        var blobName = "file.txt";

        // Act
        var result = await _testHost!.InvokeBlobAsync("ProcessBlob", content, blobName);

        // Assert
        TestContext.Current?.Output.WriteLine($"Success: {result.Success}, Error: {result.Error}");
        await Assert.That(result.Success).IsTrue();

        var processed = _processedItems!.TakeAll();
        await Assert.That(processed.Count).IsEqualTo(1);
        await Assert.That(processed[0].Contains("Hello from blob!")).IsTrue();
    }

    [Test]
    public async Task InvokeEventGridAsync_WithEventGridEvent_Succeeds()
    {
        // Arrange
        var subject = "test/subject";
        var eventGridEvent = new EventGridEvent(
            subject: subject,
            eventType: "Test.Event",
            dataVersion: "1.0",
            data: BinaryData.FromObjectAsJson(new { message = "Hello from Event Grid!" }));

        // Act
        var result = await _testHost!.InvokeEventGridAsync("ProcessEventGridEvent", eventGridEvent);

        // Assert
        TestContext.Current?.Output.WriteLine($"Success: {result.Success}, Error: {result.Error}");
        await Assert.That(result.Success).IsTrue();

        var processed = _processedItems!.TakeAll();
        await Assert.That(processed.Count).IsEqualTo(1);
        await Assert.That(processed[0]).IsEqualTo(subject);
    }
}
