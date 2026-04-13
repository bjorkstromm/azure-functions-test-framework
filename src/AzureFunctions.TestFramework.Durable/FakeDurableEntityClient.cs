using Microsoft.DurableTask;
using Microsoft.DurableTask.Client.Entities;
using Microsoft.DurableTask.Entities;

namespace AzureFunctions.TestFramework.Durable;

internal sealed class FakeDurableEntityClient : DurableEntityClient
{
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
        => throw new NotSupportedException("Use the generic GetEntityAsync<TState> overload.");

    public override AsyncPageable<EntityMetadata> GetAllEntitiesAsync(EntityQuery? filter)
        => throw new NotSupportedException("Querying all entity instances is not supported by the fake entity client.");

    public override AsyncPageable<EntityMetadata<TState>> GetAllEntitiesAsync<TState>(EntityQuery? filter)
        => throw new NotSupportedException("Querying all entity instances is not supported by the fake entity client.");

    public override Task<CleanEntityStorageResult> CleanEntityStorageAsync(
        CleanEntityStorageRequest? request,
        bool continueUntilComplete,
        CancellationToken cancellation)
        => throw new NotSupportedException("Clean entity storage is not supported by the fake entity client.");
}
