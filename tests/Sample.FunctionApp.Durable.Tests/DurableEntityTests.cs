using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Durable;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Sample.FunctionApp.Durable.Tests;

public sealed class DurableEntityTests
{
    private readonly ITestOutputHelper _output;

    public DurableEntityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task SignalEntityAsync_Add_UpdatesCounterState()
    {
        // Arrange
        await using var host = await CreateHostAsync();
        var entityId = new EntityInstanceId(nameof(Counter), "test-counter");

        // Act
        await host.SignalEntityAsync(entityId, "add", 5);
        await host.SignalEntityAsync(entityId, "add", 3);

        // Assert
        var metadata = host.GetEntity<int>(entityId);
        Assert.NotNull(metadata);
        Assert.Equal(8, metadata.State);
    }

    [Fact]
    public async Task SignalEntityAsync_Reset_ClearsCounterState()
    {
        // Arrange
        await using var host = await CreateHostAsync();
        var entityId = new EntityInstanceId(nameof(Counter), "reset-counter");

        await host.SignalEntityAsync(entityId, "add", 10);

        // Act
        await host.SignalEntityAsync(entityId, "reset");

        // Assert
        var metadata = host.GetEntity<int>(entityId);
        Assert.NotNull(metadata);
        Assert.Equal(0, metadata.State);
    }

    [Fact]
    public async Task CallEntityAsync_Get_ReturnsCurrentValue()
    {
        // Arrange
        await using var host = await CreateHostAsync();
        var entityId = new EntityInstanceId(nameof(Counter), "get-counter");

        await host.SignalEntityAsync(entityId, "add", 42);

        // Act
        var value = await host.CallEntityAsync<int>(entityId, "get");

        // Assert
        Assert.Equal(42, value);
    }

    [Fact]
    public async Task GetEntity_BeforeAnyOperation_ReturnsNull()
    {
        // Arrange
        await using var host = await CreateHostAsync();
        var entityId = new EntityInstanceId(nameof(Counter), "nonexistent");

        // Act
        var metadata = host.GetEntity<int>(entityId);

        // Assert
        Assert.Null(metadata);
    }

    [Fact]
    public async Task SignalEntityAsync_MultipleInstances_AreIsolated()
    {
        // Arrange
        await using var host = await CreateHostAsync();
        var entityA = new EntityInstanceId(nameof(Counter), "counter-a");
        var entityB = new EntityInstanceId(nameof(Counter), "counter-b");

        // Act
        await host.SignalEntityAsync(entityA, "add", 10);
        await host.SignalEntityAsync(entityB, "add", 99);

        // Assert
        var metaA = host.GetEntity<int>(entityA);
        var metaB = host.GetEntity<int>(entityB);
        Assert.Equal(10, metaA?.State);
        Assert.Equal(99, metaB?.State);
    }

    [Fact]
    public async Task DurableTaskClient_Entities_SignalAndGet_UpdatesState()
    {
        // Arrange
        await using var host = await CreateHostAsync();
        var durableClient = host.Services.GetRequiredService<FunctionsDurableClientProvider>().Client;
        var entityId = new EntityInstanceId(nameof(Counter), "client-counter");

        // Act
        await durableClient.Entities.SignalEntityAsync(entityId, "add", 7);

        // Assert
        var metadata = await durableClient.Entities.GetEntityAsync<int>(entityId);
        Assert.NotNull(metadata);
        Assert.Equal(7, metadata.State);
    }

    [Fact]
    public async Task Invoker_GetFunctions_IncludesEntityTrigger()
    {
        // Arrange
        await using var host = await CreateHostAsync();

        // Act
        var functions = host.Invoker.GetFunctions();

        // Assert
        var counter = Assert.Contains(nameof(Counter), functions);
        Assert.Equal("entityTrigger", counter.GetDurableTriggerType());
    }

    [Fact]
    public async Task OrchestratorCallsEntity_ViaContextEntities_ReturnsAccumulatedValue()
    {
        // Arrange
        await using var host = await CreateHostAsync();
        var durableClient = host.Services.GetRequiredService<FunctionsDurableClientProvider>().Client;

        // Act — orchestrator calls Counter entity twice then reads it back
        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunCounterOrchestration));
        var metadata = await durableClient.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true);

        // Assert
        Assert.Equal(Microsoft.DurableTask.Client.OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);
        Assert.Equal(15, metadata.ReadOutputAs<int>());
    }

    private Task<IFunctionsTestHost> CreateHostAsync()
    {
        return new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(DurableGreetingFunctions).Assembly)
            .WithLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new XUnitLoggerProvider(_output))))
            .WithHostBuilderFactory(Program.CreateWorkerHostBuilder)
            .ConfigureFakeDurableSupport(typeof(DurableGreetingFunctions).Assembly)
            .BuildAndStartAsync();
    }
}
