using AzureFunctions.TestFramework.Durable;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Durable;

/// <summary>
/// Unit tests for <see cref="FakeDurableEntityClient"/>, covering supported entity
/// operations and delegation to <see cref="FakeDurableEntityRunner"/>.
/// </summary>
public class FakeDurableEntityClientTests
{
    // ── Query and state operations ────────────────────────────────────────────

    [Fact]
    public async Task GetEntityAsync_NonGeneric_NoState_ReturnsNull()
    {
        using var resources = CreateResources();
        var entityId = new EntityInstanceId("MutableTestEntity", "key1");

#pragma warning disable xUnit1051
        var metadata = await resources.Client.GetEntityAsync(entityId, includeState: true, CancellationToken.None);
#pragma warning restore xUnit1051

        Assert.Null(metadata);
    }

    [Fact]
    public async Task GetEntityAsync_NonGeneric_WithState_ReturnsSerializedState()
    {
        using var resources = CreateResources();
        var entityId = new EntityInstanceId("MutableTestEntity", "key2");

#pragma warning disable xUnit1051
        await resources.Client.SignalEntityAsync(entityId, "add", 7, options: null, CancellationToken.None);
        var metadata = await resources.Client.GetEntityAsync(entityId, includeState: true, CancellationToken.None);
#pragma warning restore xUnit1051

        Assert.NotNull(metadata);
        Assert.True(metadata.IncludesState);
        Assert.Equal(7, metadata.State.ReadAs<int>());
    }

    [Fact]
    public async Task GetAllEntitiesAsync_NonGeneric_ReturnsEntities()
    {
        using var resources = CreateResources();
        var entityId = new EntityInstanceId("MutableTestEntity", "all-entities-1");

#pragma warning disable xUnit1051
        await resources.Client.SignalEntityAsync(entityId, "add", 4, options: null, CancellationToken.None);
#pragma warning restore xUnit1051

        List<Microsoft.DurableTask.Client.Entities.EntityMetadata> entities = [];
        await foreach (var metadata in resources.Client.GetAllEntitiesAsync(filter: null))
        {
            entities.Add(metadata);
        }

        Assert.Contains(entities, metadata => metadata.Id == entityId);
    }

    [Fact]
    public async Task GetAllEntitiesAsync_Generic_ReturnsTypedState()
    {
        using var resources = CreateResources();
        var entityId = new EntityInstanceId("MutableTestEntity", "all-entities-2");

#pragma warning disable xUnit1051
        await resources.Client.SignalEntityAsync(entityId, "add", 9, options: null, CancellationToken.None);
#pragma warning restore xUnit1051

        List<Microsoft.DurableTask.Client.Entities.EntityMetadata<int>> entities = [];
        await foreach (var metadata in resources.Client.GetAllEntitiesAsync<int>(
                           new Microsoft.DurableTask.Client.Entities.EntityQuery { IncludeState = true }))
        {
            entities.Add(metadata);
        }

        var selected = Assert.Single(entities, metadata => metadata.Id == entityId);
        Assert.Equal(9, selected.State);
    }

    [Fact]
    public async Task CleanEntityStorageAsync_RemovesEmptyEntities()
    {
        using var resources = CreateResources();
        var initializedEntity = new EntityInstanceId("MutableTestEntity", "stateful-clean");
        var emptyEntity = new EntityInstanceId("StatelessTestEntity", "empty-clean");

#pragma warning disable xUnit1051
        await resources.Client.SignalEntityAsync(initializedEntity, "add", 2, options: null, CancellationToken.None);
        _ = await resources.Runner.CallEntityAsync(emptyEntity, "noop", null, CancellationToken.None);
        var cleanupResult = await resources.Client.CleanEntityStorageAsync(
            request: null,
            continueUntilComplete: false,
            CancellationToken.None);
        var remainingInitialized = await resources.Client.GetEntityAsync<int>(initializedEntity, includeState: true, CancellationToken.None);
        var removedEmpty = await resources.Client.GetEntityAsync(emptyEntity, includeState: true, CancellationToken.None);
#pragma warning restore xUnit1051

        Assert.True(cleanupResult.EmptyEntitiesRemoved >= 1);
        Assert.NotNull(remainingInitialized);
        Assert.Null(removedEmpty);
    }

    // ── SignalEntityAsync (supported) ─────────────────────────────────────────

    [Fact]
    public async Task SignalEntityAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        using var resources = CreateResources();
        var entityId = new EntityInstanceId("AnyEntity", "key");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            resources.Client.SignalEntityAsync(entityId, "op", null, options: null, cts.Token));
    }

    // ── Factory ──────────────────────────────────────────────────────────────

    private static TestResources CreateResources()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        var catalog = new FakeDurableFunctionCatalog(typeof(FakeDurableEntityClientTests).Assembly);
        var entityRunnerLogger = services.GetRequiredService<ILogger<FakeDurableEntityRunner>>();
        var entityRunner = new FakeDurableEntityRunner(catalog, services, entityRunnerLogger);
        var client = new FakeDurableEntityClient(entityRunner);
        return new TestResources(client, entityRunner);
    }

    private sealed class TestResources : IDisposable
    {
        public TestResources(FakeDurableEntityClient client, FakeDurableEntityRunner runner)
        {
            Client = client;
            Runner = runner;
        }

        public FakeDurableEntityClient Client { get; }
        public FakeDurableEntityRunner Runner { get; }

        public void Dispose() => Runner.Dispose();
    }
}

public sealed class MutableTestEntity : TaskEntity<int>
{
    public int Get() => State;

    public void Add(int value) => State += value;

    [Function(nameof(MutableTestEntity))]
    public Task Run([EntityTrigger] TaskEntityDispatcher dispatcher) => dispatcher.DispatchAsync<MutableTestEntity>();
}

public sealed class StatelessTestEntity : ITaskEntity
{
    public ValueTask<object?> RunAsync(TaskEntityOperation operation)
        => ValueTask.FromResult<object?>(null);

    [Function(nameof(StatelessTestEntity))]
    public Task Run([EntityTrigger] TaskEntityDispatcher dispatcher) => dispatcher.DispatchAsync<StatelessTestEntity>();
}
