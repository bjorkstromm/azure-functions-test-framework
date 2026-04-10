using Azure.Storage.Queues.Models;
using AzureFunctions.TestFramework.Queue;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TestProject;

/// <summary>
/// Tests verifying that <c>[ServiceBusOutput]</c> and <c>[EventGridOutput]</c> bindings
/// are captured in <see cref="FunctionInvocationResult.OutputData"/>.
/// These run via queue-triggered functions that write to the respective output bindings.
/// </summary>
public abstract class OutputBindingTestsBase : TestHostTestBase
{
    private InMemoryProcessedItemsService? _processedItems;

    protected OutputBindingTestsBase(ITestOutputHelper output) : base(output) { }

    protected override async Task<IFunctionsTestHost> CreateTestHostAsync()
    {
        _processedItems = new InMemoryProcessedItemsService();
        return await CreateTestHostWithProcessedItemsAsync(_processedItems);
    }

    protected abstract Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(InMemoryProcessedItemsService processedItems);

    [Fact]
    public async Task InvokeQueueAsync_CapturesServiceBusOutputBindingData()
    {
        var messageText = "Hello ServiceBus output!";
        var message = QueuesModelFactory.QueueMessage(Guid.NewGuid().ToString(), "pop-receipt", messageText, 1);

        var result = await TestHost.InvokeQueueAsync("CreateServiceBusOutputMessage", message, TestCancellation);

        Assert.True(result.Success, $"Queue invocation failed: {result.Error}");
        var outputMessage = result.ReadOutputAs<string>("OutputMessage");
        Assert.Equal($"sb:{messageText}", outputMessage);
    }

    [Fact]
    public async Task InvokeQueueAsync_CapturesEventGridOutputBindingData()
    {
        var messageText = "Hello EventGrid output!";
        var message = QueuesModelFactory.QueueMessage(Guid.NewGuid().ToString(), "pop-receipt", messageText, 1);

        var result = await TestHost.InvokeQueueAsync("CreateEventGridOutputEvent", message, TestCancellation);

        Assert.True(result.Success, $"Queue invocation failed: {result.Error}");
        var outputEvent = result.ReadOutputAs<string>("OutputEvent");
        Assert.Equal($"eg:{messageText}", outputEvent);
    }
}
