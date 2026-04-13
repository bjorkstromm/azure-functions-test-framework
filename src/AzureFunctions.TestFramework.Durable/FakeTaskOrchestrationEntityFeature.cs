using Microsoft.DurableTask.Entities;

namespace AzureFunctions.TestFramework.Durable;

internal sealed class FakeTaskOrchestrationEntityFeature : TaskOrchestrationEntityFeature
{
    private readonly FakeDurableEntityRunner _entityRunner;

    public FakeTaskOrchestrationEntityFeature(FakeDurableEntityRunner entityRunner)
    {
        _entityRunner = entityRunner;
    }

    public override async Task<TResult> CallEntityAsync<TResult>(
        EntityInstanceId id,
        string operationName,
        object? input,
        CallEntityOptions? options)
    {
        var result = await _entityRunner.CallEntityAsync(id, operationName, input, CancellationToken.None)
            .ConfigureAwait(false);
        return (TResult?)FakeDurableOrchestrationRunner.ConvertValue(result, typeof(TResult))!;
    }

    public override async Task CallEntityAsync(
        EntityInstanceId id,
        string operationName,
        object? input,
        CallEntityOptions? options)
    {
        await _entityRunner.CallEntityAsync(id, operationName, input, CancellationToken.None)
            .ConfigureAwait(false);
    }

    public override Task SignalEntityAsync(
        EntityInstanceId id,
        string operationName,
        object? input,
        SignalEntityOptions? options)
        => _entityRunner.SignalEntityAsync(id, operationName, input, options, CancellationToken.None);

    public override Task<IAsyncDisposable> LockEntitiesAsync(IEnumerable<EntityInstanceId> entityIds)
        => throw new NotSupportedException("Entity locking from orchestrations is not supported by the fake.");

    public override bool InCriticalSection(out IReadOnlyList<EntityInstanceId> entityIds)
    {
        entityIds = [];
        return false;
    }
}
