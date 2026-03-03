using Azure.Storage.Queues.Models;
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Queue;
using Sample.FunctionApp;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Sample.FunctionApp.Tests;

/// <summary>
/// Integration tests for queue-triggered Azure Functions using <see cref="FunctionsTestHost"/>.
/// Demonstrates invoking queue functions via <see cref="FunctionsTestHostQueueExtensions.InvokeQueueAsync"/>.
/// </summary>
public class QueueFunctionsTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IFunctionsTestHost? _testHost;

    public QueueFunctionsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        var builder = new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(QueueFunction).Assembly);

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
        // Arrange
        var body = BinaryData.FromString("Hello from test queue");
        var message = QueuesModelFactory.QueueMessage(
            messageId: Guid.NewGuid().ToString(),
            popReceipt: "pop-receipt",
            body: body,
            dequeueCount: 1);

        // Act
        var result = await _testHost!.InvokeQueueAsync("ProcessQueueMessage", message);

        _output.WriteLine($"InvocationId: {result.InvocationId}");
        _output.WriteLine($"Success: {result.Success}");
        if (result.Error != null)
        {
            _output.WriteLine($"Error: {result.Error}");
        }

        // Assert
        Assert.True(result.Success, $"Expected invocation to succeed but got error: {result.Error}");
    }

    [Fact]
    public async Task InvokeQueueAsync_WithJsonMessage_Succeeds()
    {
        // Arrange — simulate a JSON payload that a real queue consumer might send
        var json = """{"id":42,"name":"test-item"}""";
        var body = BinaryData.FromString(json);
        var message = QueuesModelFactory.QueueMessage(
            messageId: Guid.NewGuid().ToString(),
            popReceipt: "pop-receipt",
            body: body,
            dequeueCount: 1);

        // Act
        var result = await _testHost!.InvokeQueueAsync("ProcessQueueMessage", message);

        _output.WriteLine($"InvocationId: {result.InvocationId}");
        _output.WriteLine($"Success: {result.Success}");

        // Assert
        Assert.True(result.Success, $"Expected invocation to succeed but got error: {result.Error}");
    }

    [Fact]
    public async Task InvokeQueueAsync_UnknownFunction_ThrowsInvalidOperationException()
    {
        var body = BinaryData.FromString("test");
        var message = QueuesModelFactory.QueueMessage(
            messageId: Guid.NewGuid().ToString(),
            popReceipt: "pop-receipt",
            body: body,
            dequeueCount: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _testHost!.InvokeQueueAsync("NonExistentQueueFunction", message));
    }
}
