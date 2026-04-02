using System.Collections.Concurrent;
using Microsoft.DurableTask.Client.Entities;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzureFunctions.TestFramework.Durable;

internal sealed class FakeDurableEntityRunner : IDisposable
{
    private readonly FakeDurableFunctionCatalog _catalog;
    private readonly ConcurrentDictionary<EntityInstanceId, FakeEntityInstanceState> _entities = new();
    private readonly ILogger<FakeDurableEntityRunner> _logger;
    private readonly IServiceProvider _serviceProvider;

    public FakeDurableEntityRunner(
        FakeDurableFunctionCatalog catalog,
        IServiceProvider serviceProvider,
        ILogger<FakeDurableEntityRunner> logger)
    {
        _catalog = catalog;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public void Dispose()
    {
        foreach (var instance in _entities.Values)
        {
            instance.Dispose();
        }
    }

    /// <summary>Signals an entity (fire-and-forget) and awaits serial execution.</summary>
    public async Task SignalEntityAsync(
        EntityInstanceId entityId,
        string operationName,
        object? input,
        CancellationToken cancellationToken)
    {
        var instance = _entities.GetOrAdd(entityId, _ => new FakeEntityInstanceState());
        await instance.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ExecuteOperationAsync(entityId, instance, operationName, input, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            instance.Semaphore.Release();
        }
    }

    /// <summary>Calls an entity and returns its result.</summary>
    public async Task<object?> CallEntityAsync(
        EntityInstanceId entityId,
        string operationName,
        object? input,
        CancellationToken cancellationToken)
    {
        var instance = _entities.GetOrAdd(entityId, _ => new FakeEntityInstanceState());
        await instance.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ExecuteOperationAsync(entityId, instance, operationName, input, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            instance.Semaphore.Release();
        }
    }

    /// <summary>Retrieves the current state of an entity.</summary>
    public EntityMetadata<TState> GetEntity<TState>(EntityInstanceId entityId)
    {
        if (!_entities.TryGetValue(entityId, out var instance) || !instance.EntityState.HasState)
        {
            // Entities always exist (virtual-actor model). Return default state for uninitialized entities.
            // For value types this is 0/false/etc.; for reference types this is null (suppressed with !).
            return new EntityMetadata<TState>(entityId, default!);
        }

        return new EntityMetadata<TState>(entityId, instance.EntityState.GetState<TState>()!)
        {
            LastModifiedTime = instance.LastModifiedTime,
        };
    }

    private async Task<object?> ExecuteOperationAsync(
        EntityInstanceId entityId,
        FakeEntityInstanceState instance,
        string operationName,
        object? input,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing entity operation {Operation} on {EntityId}", operationName, entityId);
        var entityType = _catalog.GetEntityType(entityId.Name);

        await using var scope = _serviceProvider.CreateAsyncScope();
        var context = new FakeTaskEntityContext(entityId, this, _logger);
        var operation = new FakeTaskEntityOperation(operationName, input, context, instance.EntityState);

        var entity = (ITaskEntity)ActivatorUtilities.GetServiceOrCreateInstance(scope.ServiceProvider, entityType);
        var result = await entity.RunAsync(operation).ConfigureAwait(false);
        instance.LastModifiedTime = DateTimeOffset.UtcNow;

        _logger.LogInformation("Completed entity operation {Operation} on {EntityId}", operationName, entityId);
        return result;
    }

    private sealed class FakeEntityInstanceState : IDisposable
    {
        public FakeTaskEntityState EntityState { get; } = new FakeTaskEntityState();
        public DateTimeOffset LastModifiedTime { get; set; } = DateTimeOffset.UtcNow;
        public SemaphoreSlim Semaphore { get; } = new SemaphoreSlim(1, 1);

        public void Dispose() => Semaphore.Dispose();
    }
}
