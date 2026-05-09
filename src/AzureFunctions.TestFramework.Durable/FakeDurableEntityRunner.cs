using System.Collections.Concurrent;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.Entities;
using Microsoft.DurableTask.Converters;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzureFunctions.TestFramework.Durable;

internal sealed class FakeDurableEntityRunner : IDisposable
{
    private readonly FakeDurableFunctionCatalog _catalog;
    private readonly JsonDataConverter _dataConverter = JsonDataConverter.Default;
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
        // Only cancel — do not dispose. Background delayed-signal tasks hold a reference to
        // _shutdownCts.Token; disposing while a task is still running can cause an
        // ObjectDisposedException on the fire-and-forget task (unobserved).
        _shutdownCts.Cancel();

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
            cancellationToken.ThrowIfCancellationRequested();
            _ = Task.Run(() => DelayedSignalAsync(entityId, operationName, input, delay))
                .ContinueWith(
                    t => _logger.LogError(t.Exception, "Unhandled exception in delayed entity signal {Operation} on {EntityId}", operationName, entityId),
                    TaskContinuationOptions.OnlyOnFaulted);
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
        TimeSpan delay)
    {
        // Only _shutdownCts is used: the caller's token cancelled the enqueue act, which already
        // completed (we returned Task.CompletedTask). The delayed execution should only be
        // cancelled when the host shuts down.
        try
        {
            _logger.LogInformation("Scheduling entity signal {Operation} on {EntityId} with delay {Delay}", operationName, entityId, delay);
            await Task.Delay(delay, _shutdownCts.Token).ConfigureAwait(false);
            await ExecuteSignalAsync(entityId, operationName, input, _shutdownCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Silently swallow — the host is shutting down.
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

    // Task.Delay uses int milliseconds internally; clamp to prevent ArgumentOutOfRangeException
    // for far-future SignalTime values.
    private static readonly TimeSpan MaxDelay = TimeSpan.FromMilliseconds(int.MaxValue);

    internal static TimeSpan ComputeDelay(SignalEntityOptions? options)
    {
        if (options?.SignalTime is not { } signalTime)
            return TimeSpan.Zero;

        return ClampDelay(signalTime - DateTimeOffset.UtcNow);
    }

    private static TimeSpan ClampDelay(TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero) return TimeSpan.Zero;
        return delay < MaxDelay ? delay : MaxDelay;
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

    public EntityRawState? GetEntityRaw(EntityInstanceId entityId)
    {
        if (!_entities.TryGetValue(entityId, out var instance) || !instance.EntityState.HasState)
        {
            return null;
        }

        return new EntityRawState(instance.EntityState.SerializedState!, instance.LastModifiedTime);
    }

    public IReadOnlyList<EntityMetadata> GetAllEntities(EntityQuery? filter)
    {
        var includeState = filter?.IncludeState ?? false;
        return ApplyEntityQueryFilter(filter)
            .Select(entry =>
            {
                SerializedData? serializedData = includeState && entry.Value.EntityState.HasState
                    ? new SerializedData(entry.Value.EntityState.SerializedState!, _dataConverter)
                    : null;

                return new EntityMetadata(entry.Key, serializedData)
                {
                    LastModifiedTime = entry.Value.LastModifiedTime,
                };
            })
            .ToArray();
    }

    public IReadOnlyList<EntityMetadata<TState>> GetAllEntities<TState>(EntityQuery? filter)
    {
        var includeState = filter?.IncludeState ?? false;
        return ApplyEntityQueryFilter(filter)
            .Select(entry =>
            {
                return includeState && entry.Value.EntityState.HasState
                    ? new EntityMetadata<TState>(entry.Key, entry.Value.EntityState.GetState<TState>()!)
                    {
                        LastModifiedTime = entry.Value.LastModifiedTime,
                    }
                    : new EntityMetadata<TState>(entry.Key)
                    {
                        LastModifiedTime = entry.Value.LastModifiedTime,
                    };
            })
            .ToArray();
    }

    public int CleanEntityStorage(CleanEntityStorageRequest? request)
    {
        var removeEmptyEntities = request?.RemoveEmptyEntities ?? true;
        if (!removeEmptyEntities)
        {
            return 0;
        }

        var removedCount = 0;
        foreach (var entry in _entities.ToArray())
        {
            if (entry.Value.EntityState.HasState)
            {
                continue;
            }

            if (_entities.TryRemove(entry))
            {
                entry.Value.Dispose();
                removedCount++;
            }
        }

        return removedCount;
    }

    private IEnumerable<KeyValuePair<EntityInstanceId, FakeEntityInstanceState>> ApplyEntityQueryFilter(EntityQuery? filter)
    {
        IEnumerable<KeyValuePair<EntityInstanceId, FakeEntityInstanceState>> entities = _entities.ToArray();

        if (!string.IsNullOrWhiteSpace(filter?.InstanceIdStartsWith))
        {
            entities = entities.Where(entry =>
                entry.Key.Key.StartsWith(filter.InstanceIdStartsWith, StringComparison.Ordinal));
        }

        if (filter?.LastModifiedFrom is { } lastModifiedFrom)
        {
            entities = entities.Where(entry => entry.Value.LastModifiedTime >= lastModifiedFrom);
        }

        if (filter?.LastModifiedTo is { } lastModifiedTo)
        {
            entities = entities.Where(entry => entry.Value.LastModifiedTime <= lastModifiedTo);
        }

        if (!(filter?.IncludeTransient ?? false))
        {
            entities = entities.Where(entry => entry.Value.EntityState.HasState);
        }

        if (filter?.PageSize is > 0)
        {
            entities = entities.Take(filter.PageSize.Value);
        }

        return entities;
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
        var context = new FakeTaskEntityContext(entityId, this, _logger, ScheduleOrchestration);
        var operation = new FakeTaskEntityOperation(operationName, input, context, instance.EntityState);

        var entity = (ITaskEntity)ActivatorUtilities.GetServiceOrCreateInstance(scope.ServiceProvider, entityType);
        var result = await entity.RunAsync(operation).ConfigureAwait(false);
        instance.LastModifiedTime = DateTimeOffset.UtcNow;

        _logger.LogInformation("Completed entity operation {Operation} on {EntityId}", operationName, entityId);
        return result;
    }

    private string ScheduleOrchestration(
        string orchestrationName,
        object? input,
        StartOrchestrationOptions? options)
    {
        var instanceId = options?.InstanceId ?? Guid.NewGuid().ToString("N");
        var scheduleOptions = new StartOrchestrationOptions
        {
            InstanceId = instanceId,
            StartAt = options?.StartAt,
            Tags = options?.Tags ?? new Dictionary<string, string>(),
            Version = options?.Version,
            DedupeStatuses = options?.DedupeStatuses ?? Array.Empty<string>(),
        };

        _ = Task.Run(async () =>
            {
                var client = _serviceProvider.GetRequiredService<FakeDurableTaskClient>();
                await client.ScheduleNewOrchestrationInstanceAsync(
                        new TaskName(orchestrationName),
                        input,
                        scheduleOptions,
                        _shutdownCts.Token)
                    .ConfigureAwait(false);
            }, _shutdownCts.Token)
            .ContinueWith(
                t => _logger.LogError(
                    t.Exception,
                    "Unhandled exception while scheduling orchestration {OrchestrationName} from entity context",
                    orchestrationName),
                TaskContinuationOptions.OnlyOnFaulted);

        return instanceId;
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

    public readonly record struct EntityRawState(string SerializedState, DateTimeOffset LastModifiedTime);
}
