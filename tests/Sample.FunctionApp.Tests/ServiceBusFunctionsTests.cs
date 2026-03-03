using Azure.Messaging.ServiceBus;
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.ServiceBus;
using Sample.FunctionApp;
using Xunit;
using Xunit.Abstractions;

namespace Sample.FunctionApp.Tests;

/// <summary>
/// Integration tests for Service Bus–triggered Azure Functions using <see cref="FunctionsTestHost"/>.
/// Demonstrates invoking Service Bus functions via
/// <see cref="FunctionsTestHostServiceBusExtensions.InvokeServiceBusAsync"/>.
/// </summary>
public class ServiceBusFunctionsTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IFunctionsTestHost? _testHost;

    public ServiceBusFunctionsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        var builder = new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(OrderMessageFunction).Assembly);

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
    public async Task InvokeServiceBusAsync_WithStringBody_Succeeds()
    {
        // Arrange
        var message = new ServiceBusMessage("Hello from test!");

        // Act
        var result = await _testHost!.InvokeServiceBusAsync("ProcessOrderMessage", message);

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
    public async Task InvokeServiceBusAsync_WithJsonBody_Succeeds()
    {
        // Arrange — simulate a JSON message payload
        var message = new ServiceBusMessage("{\"orderId\": \"abc123\", \"quantity\": 5}")
        {
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString(),
            CorrelationId = "test-correlation-id"
        };

        // Act
        var result = await _testHost!.InvokeServiceBusAsync("ProcessOrderMessage", message);

        _output.WriteLine($"InvocationId: {result.InvocationId}");
        _output.WriteLine($"Success: {result.Success}");

        // Assert
        Assert.True(result.Success, $"Expected invocation to succeed but got error: {result.Error}");
    }

    [Fact]
    public async Task InvokeServiceBusAsync_UnknownFunction_ThrowsInvalidOperationException()
    {
        var message = new ServiceBusMessage("test");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _testHost!.InvokeServiceBusAsync("NonExistentServiceBusFunction", message));
    }
}
