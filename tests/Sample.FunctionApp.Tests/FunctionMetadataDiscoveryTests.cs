using AzureFunctions.TestFramework.Core;
using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;
using Microsoft.Extensions.DependencyInjection;
using Sample.FunctionApp;
using Xunit;
using Xunit.Abstractions;

namespace Sample.FunctionApp.Tests;

/// <summary>
/// Integration tests verifying that <see cref="IFunctionInvoker.GetFunctions"/> returns
/// correct metadata for all functions discovered from the sample function app.
/// </summary>
public class FunctionMetadataDiscoveryTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IFunctionsTestHost? _testHost;

    public FunctionMetadataDiscoveryTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        var builder = new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .ConfigureServices(services => services.AddSingleton<ITodoService, InMemoryTodoService>());

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
    public void GetFunctions_ReturnsAllDiscoveredFunctions()
    {
        // Act
        var functions = _testHost!.Invoker.GetFunctions();

        _output.WriteLine($"Discovered {functions.Count} function(s):");
        foreach (var (name, metadata) in functions)
        {
            _output.WriteLine($"  {name} (ID: {metadata.FunctionId}, EntryPoint: {metadata.EntryPoint})");
        }

        // Assert — sample app has 10 functions: 5 Todo HTTP, 2 Health HTTP (Health + Echo),
        // 1 HeartbeatTimer, 1 ProcessOrderMessage (ServiceBus), 1 ProcessQueueMessage (Queue)
        Assert.NotNull(functions);
        Assert.Equal(10, functions.Count);
    }

    [Fact]
    public void GetFunctions_ReturnsHttpFunctionsWithCorrectBindings()
    {
        // Act
        var functions = _testHost!.Invoker.GetFunctions();

        // Assert — GetTodos should have an httpTrigger binding in RawBindings
        Assert.True(functions.TryGetValue("GetTodos", out var getTodos), "GetTodos not found");
        Assert.NotEmpty(getTodos!.FunctionId!);
        Assert.NotEmpty(getTodos.EntryPoint!);

        // RawBindings contains JSON strings; verify the httpTrigger binding is present
        Assert.Contains(getTodos.RawBindings!, b =>
            b.Contains("httpTrigger", StringComparison.OrdinalIgnoreCase) &&
            b.Contains("\"req\"", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetFunctions_ReturnsTimerFunctionWithCorrectBinding()
    {
        // Act
        var functions = _testHost!.Invoker.GetFunctions();

        // Assert — HeartbeatTimer should have a timerTrigger binding in RawBindings
        Assert.True(functions.TryGetValue("HeartbeatTimer", out var heartbeat), "HeartbeatTimer not found");
        Assert.NotEmpty(heartbeat!.FunctionId!);

        Assert.Contains(heartbeat.RawBindings!, b =>
            b.Contains("timerTrigger", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetFunctions_MetadataHasExpectedFields()
    {
        // Act
        var functions = _testHost!.Invoker.GetFunctions();

        // Assert — every function should have non-empty Name, FunctionId, and at least one raw binding
        foreach (var (name, metadata) in functions)
        {
            Assert.False(string.IsNullOrEmpty(metadata.Name), $"{name}: Name should not be empty");
            Assert.False(string.IsNullOrEmpty(metadata.FunctionId), $"{name}: FunctionId should not be empty");
            Assert.NotEmpty(metadata.RawBindings!);
        }
    }
}
