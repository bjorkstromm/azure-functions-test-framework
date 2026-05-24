using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.Entities;
using Microsoft.DurableTask.Converters;
using Microsoft.DurableTask.Entities;

namespace AzureFunctions.TestFramework.Durable;

internal sealed class FakeDurableEntityClient : DurableEntityClient
{
    private readonly JsonDataConverter _dataConverter = JsonDataConverter.Default;
    private readonly FakeDurableEntityRunner _entityRunner;

    public FakeDurableEntityClient(FakeDurableEntityRunner entityRunner)
        : base("FAKE")
    {
        _entityRunner = entityRunner;
    }

    public override Task SignalEntityAsync(
        EntityInstanceId id,
        string operationName,
        object? input,
        SignalEntityOptions? options,
        CancellationToken cancellation)
        => _entityRunner.SignalEntityAsync(id, operationName, input, options, cancellation);

    public override Task<EntityMetadata<TState>?> GetEntityAsync<TState>(
        EntityInstanceId id,
        bool includeState,
        CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        return Task.FromResult<EntityMetadata<TState>?>(_entityRunner.GetEntity<TState>(id));
    }

    public override Task<EntityMetadata?> GetEntityAsync(
        EntityInstanceId id,
        bool includeState,
        CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var raw = _entityRunner.GetEntityRaw(id);
        if (raw is null)
        {
            return Task.FromResult<EntityMetadata?>(null);
        }

        SerializedData? state = includeState
            ? new SerializedData(raw.Value.SerializedState, _dataConverter)
            : null;

        EntityMetadata metadata = new(id, state)
        {
            LastModifiedTime = raw.Value.LastModifiedTime,
        };

        return Task.FromResult<EntityMetadata?>(metadata);
    }

    public override AsyncPageable<EntityMetadata> GetAllEntitiesAsync(EntityQuery? filter)
    {
        var metadata = _entityRunner.GetAllEntities(filter);
        return new SinglePageAsyncPageable<EntityMetadata>(metadata);
    }

    public override AsyncPageable<EntityMetadata<TState>> GetAllEntitiesAsync<TState>(EntityQuery? filter)
    {
        var metadata = _entityRunner.GetAllEntities<TState>(filter);
        return new SinglePageAsyncPageable<EntityMetadata<TState>>(metadata);
    }

    public override Task<CleanEntityStorageResult> CleanEntityStorageAsync(
        CleanEntityStorageRequest? request,
        bool continueUntilComplete,
        CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var removedCount = _entityRunner.CleanEntityStorage(request);
        return Task.FromResult(new CleanEntityStorageResult
        {
            EmptyEntitiesRemoved = removedCount,
            OrphanedLocksReleased = 0,
            ContinuationToken = null,
        });
    }
}
