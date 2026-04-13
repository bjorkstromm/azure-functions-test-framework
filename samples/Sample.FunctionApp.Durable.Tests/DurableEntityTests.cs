using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Durable;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Sample.FunctionApp.Durable.Tests;

public sealed class DurableEntityTests
{
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

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
        await host.SignalEntityAsync(entityId, "add", 5, TestCancellation);
        await host.SignalEntityAsync(entityId, "add", 3, TestCancellation);

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

        await host.SignalEntityAsync(entityId, "add", 10, TestCancellation);

        // Act
        await host.SignalEntityAsync(entityId, "reset", cancellationToken: TestCancellation);

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

        await host.SignalEntityAsync(entityId, "add", 42, TestCancellation);

        // Act
        var value = await host.CallEntityAsync<int>(entityId, "get", cancellationToken: TestCancellation);

        // Assert
        Assert.Equal(42, value);
    }

    [Fact]
    public async Task GetEntity_BeforeAnyOperation_ReturnsEmptyState()
    {
        // Arrange
        await using var host = await CreateHostAsync();
        var entityId = new EntityInstanceId(nameof(Counter), "nonexistent");

        // Act
        var metadata = host.GetEntity<int>(entityId);

        // Assert
        Assert.NotNull(metadata);
        Assert.True(metadata.IncludesState);
        Assert.Equal(0, metadata.State);
    }

    [Fact]
    public async Task GetEntity_WithReferenceTypeState_ReturnsNonNullDefault()
    {
        // Arrange — reference-type TState must not result in IncludesState=false
        await using var host = await CreateHostAsync();
        var entityId = new EntityInstanceId(nameof(Counter), "ref-type-test");

        // Act
        var metadata = host.GetEntity<List<string>>(entityId);

        // Assert
        Assert.NotNull(metadata);
        Assert.True(metadata.IncludesState);
        Assert.NotNull(metadata.State);
        Assert.Empty(metadata.State);
    }

    [Fact]
    public async Task SignalEntityAsync_WithFutureSignalTime_DelaysExecution()
    {
        // Arrange
        await using var host = await CreateHostAsync();
        var durableClient = host.Services.GetRequiredService<FunctionsDurableClientProvider>().Client;
        var entityId = new EntityInstanceId(nameof(Counter), "delayed-counter");

        // Use a 5-second delay so the "before" assertion is reliably before the signal fires,
        // even on a slow/loaded machine.
        var options = new SignalEntityOptions { SignalTime = DateTimeOffset.UtcNow.AddSeconds(5) };

        // Act — immediate signal
        await host.SignalEntityAsync(entityId, "add", 7, TestCancellation);

        // Signal with future SignalTime — returns immediately (fire-and-forget)
#pragma warning disable xUnit1051
        await durableClient.Entities.SignalEntityAsync(entityId, "add", 10, options);
#pragma warning restore xUnit1051

        // Assert — only the immediate signal should be reflected (delayed signal hasn't fired yet)
        var metadataBefore = host.GetEntity<int>(entityId);
        Assert.Equal(7, metadataBefore.State);

        // Poll until the delayed signal fires (up to 15s to avoid flakiness on loaded CI).
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (host.GetEntity<int>(entityId).State != 17)
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, "Timed out waiting for delayed entity signal to execute.");
            await Task.Delay(50, TestCancellation);
        }
    }

    [Fact]
    public async Task SignalEntityAsync_WithPastSignalTime_ExecutesImmediately()
    {
        // Arrange
        await using var host = await CreateHostAsync();
        var durableClient = host.Services.GetRequiredService<FunctionsDurableClientProvider>().Client;
        var entityId = new EntityInstanceId(nameof(Counter), "past-signal-counter");
        var options = new SignalEntityOptions { SignalTime = DateTimeOffset.UtcNow.AddSeconds(-1) };

        // Act — signal with past SignalTime should execute immediately
#pragma warning disable xUnit1051
        await durableClient.Entities.SignalEntityAsync(entityId, "add", 5, options);
#pragma warning restore xUnit1051

        // Assert
        var metadata = host.GetEntity<int>(entityId);
        Assert.Equal(5, metadata.State);
    }

    [Fact]
    public async Task SignalEntityAsync_WithSignalTime_CancelledOnDispose_DoesNotThrow()
    {
        // Arrange
        var host = await CreateHostAsync();
        var durableClient = host.Services.GetRequiredService<FunctionsDurableClientProvider>().Client;
        var entityId = new EntityInstanceId(nameof(Counter), "cancelled-counter");
        var options = new SignalEntityOptions { SignalTime = DateTimeOffset.UtcNow.AddSeconds(30) };

        // Act — schedule a far-future signal, then dispose the host (which disposes the runner)
#pragma warning disable xUnit1051
        await durableClient.Entities.SignalEntityAsync(entityId, "add", 99, options);
#pragma warning restore xUnit1051
        await host.DisposeAsync();

        // Assert — no exception thrown; the delayed signal was cancelled on shutdown
    }

    [Fact]
    public async Task SignalEntityAsync_MultipleInstances_AreIsolated()
    {
        // Arrange
        await using var host = await CreateHostAsync();
        var entityA = new EntityInstanceId(nameof(Counter), "counter-a");
        var entityB = new EntityInstanceId(nameof(Counter), "counter-b");

        // Act
        await host.SignalEntityAsync(entityA, "add", 10, TestCancellation);
        await host.SignalEntityAsync(entityB, "add", 99, TestCancellation);

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
#pragma warning disable xUnit1051 // EntityClient.SignalEntityAsync has no CancellationToken overload in this DurableTask package version.
        await durableClient.Entities.SignalEntityAsync(entityId, "add", 7);
#pragma warning restore xUnit1051

        // Assert
#pragma warning disable xUnit1051 // EntityClient.GetEntityAsync has no CancellationToken overload in this DurableTask package version.
        var metadata = await durableClient.Entities.GetEntityAsync<int>(entityId);
#pragma warning restore xUnit1051
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
#pragma warning disable xUnit1051 // ScheduleNewOrchestrationInstanceAsync has no CancellationToken overload on DurableTaskClient.
        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunCounterOrchestration));
#pragma warning restore xUnit1051
#pragma warning disable xUnit1051 // DurableTaskClient.WaitForInstanceCompletionAsync has no CancellationToken overload in this package version.
        var metadata = await durableClient.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true);
#pragma warning restore xUnit1051

        // Assert
        Assert.Equal(Microsoft.DurableTask.Client.OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);
        Assert.Equal(15, metadata.ReadOutputAs<int>());
    }

    private Task<IFunctionsTestHost> CreateHostAsync()
    {
        return new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(DurableGreetingFunctions).Assembly)
            .WithLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new XUnitLoggerProvider(_output))))
            .WithHostBuilderFactory(args => new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureServices(s => s.AddSingleton<GreetingFormatter>()))
            .ConfigureFakeDurableSupport(typeof(DurableGreetingFunctions).Assembly)
            .BuildAndStartAsync(TestCancellation);
    }
}
