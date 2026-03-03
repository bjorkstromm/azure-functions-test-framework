using AzureFunctions.TestFramework.Core;
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

        // Assert — sample app has 8 functions: 5 Todo HTTP, 2 Health HTTP (Health + Echo), 1 HeartbeatTimer
        Assert.NotNull(functions);
        Assert.Equal(8, functions.Count);
    }

    [Fact]
    public void GetFunctions_ReturnsHttpFunctionsWithCorrectBindings()
    {
        // Act
        var functions = _testHost!.Invoker.GetFunctions();

        // Assert — GetTodos should have an httpTrigger binding
        Assert.True(functions.TryGetValue("GetTodos", out var getTodos), "GetTodos not found");
        Assert.NotEmpty(getTodos!.FunctionId);
        Assert.NotEmpty(getTodos.EntryPoint);

        var trigger = getTodos.Bindings.FirstOrDefault(b =>
            b.Type.Equals("httpTrigger", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(trigger);
        Assert.Equal("req", trigger!.Name);
    }

    [Fact]
    public void GetFunctions_ReturnsTimerFunctionWithCorrectBinding()
    {
        // Act
        var functions = _testHost!.Invoker.GetFunctions();

        // Assert — HeartbeatTimer should have a timerTrigger binding
        Assert.True(functions.TryGetValue("HeartbeatTimer", out var heartbeat), "HeartbeatTimer not found");
        Assert.NotEmpty(heartbeat!.FunctionId);

        var trigger = heartbeat.Bindings.FirstOrDefault(b =>
            b.Type.Equals("timerTrigger", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(trigger);
    }

    [Fact]
    public void GetFunctions_MetadataHasExpectedFields()
    {
        // Act
        var functions = _testHost!.Invoker.GetFunctions();

        // Assert — every function should have non-empty Name, FunctionId, and at least one binding
        foreach (var (name, metadata) in functions)
        {
            Assert.False(string.IsNullOrEmpty(metadata.Name), $"{name}: Name should not be empty");
            Assert.False(string.IsNullOrEmpty(metadata.FunctionId), $"{name}: FunctionId should not be empty");
            Assert.NotEmpty(metadata.Bindings);
        }
    }
}
