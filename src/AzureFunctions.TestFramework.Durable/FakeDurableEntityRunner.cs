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
    private readonly CancellationTokenSource _shutdownCts = new();

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
        _shutdownCts.Cancel();
        _shutdownCts.Dispose();

        foreach (var instance in _entities.Values)
        {
            instance.Dispose();
        }
    }

    /// <summary>Signals an entity (fire-and-forget) and awaits serial execution.</summary>
    public Task SignalEntityAsync(
        EntityInstanceId entityId,
        string operationName,
        object? input,
        SignalEntityOptions? options,
        CancellationToken cancellationToken)
    {
        var delay = ComputeDelay(options);
        if (delay > TimeSpan.Zero)
        {
            _ = Task.Run(() => DelayedSignalAsync(entityId, operationName, input, delay, cancellationToken));
            return Task.CompletedTask;
        }

        return ExecuteSignalAsync(entityId, operationName, input, cancellationToken);
    }

    /// <summary>Overload without options for backward compatibility.</summary>
    public Task SignalEntityAsync(
        EntityInstanceId entityId,
        string operationName,
        object? input,
        CancellationToken cancellationToken)
        => SignalEntityAsync(entityId, operationName, input, options: null, cancellationToken);

    private async Task DelayedSignalAsync(
        EntityInstanceId entityId,
        string operationName,
        object? input,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdownCts.Token);
        try
        {
            _logger.LogInformation("Scheduling entity signal {Operation} on {EntityId} with delay {Delay}", operationName, entityId, delay);
            await Task.Delay(delay, linkedCts.Token).ConfigureAwait(false);
            await ExecuteSignalAsync(entityId, operationName, input, linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Silently swallow cancellation — the host is shutting down or the caller cancelled.
        }
    }

    private async Task ExecuteSignalAsync(
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

    private static TimeSpan ComputeDelay(SignalEntityOptions? options)
    {
        if (options?.SignalTime is { } signalTime)
        {
            var delay = signalTime - DateTimeOffset.UtcNow;
            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }

        return TimeSpan.Zero;
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
            // Use Activator for reference types so EntityMetadata.IncludesState is true (it checks "state is not null").
            return new EntityMetadata<TState>(entityId, CreateDefaultState<TState>());
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

    /// <summary>
    /// Creates a default state value for <typeparamref name="TState"/>.
    /// For value types, returns <c>default</c> (e.g. 0, false).
    /// For reference types, creates a new instance via <see cref="Activator"/> so that
    /// <see cref="EntityMetadata{TState}.IncludesState"/> is <c>true</c>.
    /// </summary>
    private static TState CreateDefaultState<TState>()
    {
        if (typeof(TState).IsValueType)
        {
            return default!;
        }

        try
        {
            return Activator.CreateInstance<TState>();
        }
        catch (MissingMethodException)
        {
            // Type has no parameterless constructor; fall back to null.
            return default!;
        }
    }

    private sealed class FakeEntityInstanceState : IDisposable
    {
        public FakeTaskEntityState EntityState { get; } = new FakeTaskEntityState();
        public DateTimeOffset LastModifiedTime { get; set; } = DateTimeOffset.UtcNow;
        public SemaphoreSlim Semaphore { get; } = new SemaphoreSlim(1, 1);

        public void Dispose() => Semaphore.Dispose();
    }
}
