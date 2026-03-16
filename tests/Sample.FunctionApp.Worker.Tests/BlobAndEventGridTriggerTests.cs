using Azure.Messaging.EventGrid;
using AzureFunctions.TestFramework.Blob;
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.EventGrid;
using Microsoft.Extensions.DependencyInjection;
using Sample.FunctionApp.Worker;
using Xunit;
using Xunit.Abstractions;

namespace Sample.FunctionApp.Worker.Tests;

/// <summary>
/// Integration tests for blob-triggered and Event Grid–triggered functions.
/// </summary>
public class BlobAndEventGridTriggerTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IFunctionsTestHost? _testHost;
    private InMemoryProcessedItemsService? _processedItems;

    public BlobAndEventGridTriggerTests(ITestOutputHelper output)
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
    public async Task InvokeBlobAsync_WithTextContent_Succeeds()
    {
        var content = BinaryData.FromString("Hello from blob!");
        var blobName = "file.txt";

        var result = await _testHost!.InvokeBlobAsync("ProcessBlob", content, blobName);

        _output.WriteLine($"Success: {result.Success}, Error: {result.Error}");
        Assert.True(result.Success, $"Blob invocation failed: {result.Error}");

        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Contains("Hello from blob!", processed[0]);
    }

    [Fact]
    public async Task InvokeEventGridAsync_WithEventGridEvent_Succeeds()
    {
        var subject = "test/subject";
        var eventGridEvent = new EventGridEvent(
            subject: subject,
            eventType: "Test.Event",
            dataVersion: "1.0",
            data: BinaryData.FromObjectAsJson(new { message = "Hello from Event Grid!" }));

        var result = await _testHost!.InvokeEventGridAsync("ProcessEventGridEvent", eventGridEvent);

        _output.WriteLine($"Success: {result.Success}, Error: {result.Error}");
        Assert.True(result.Success, $"Event Grid invocation failed: {result.Error}");

        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(subject, processed[0]);
    }
}
