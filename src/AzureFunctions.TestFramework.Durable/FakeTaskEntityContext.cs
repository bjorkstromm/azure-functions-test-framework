using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;

namespace AzureFunctions.TestFramework.Durable;

internal sealed class FakeTaskEntityContext : TaskEntityContext
{
    private readonly FakeDurableEntityRunner _entityRunner;

    public FakeTaskEntityContext(EntityInstanceId entityId, FakeDurableEntityRunner entityRunner)
    {
        Id = entityId;
        _entityRunner = entityRunner;
    }

    public override EntityInstanceId Id { get; }

    public override void SignalEntity(EntityInstanceId id, string operationName, object? input, SignalEntityOptions? options)
    {
        // Fire-and-forget: start a background task so the calling entity is not blocked.
        _ = Task.Run(() => _entityRunner.SignalEntityAsync(id, operationName, input, CancellationToken.None));
    }

    public override string ScheduleNewOrchestration(TaskName name, object? input, StartOrchestrationOptions? options)
        => throw new NotSupportedException("Scheduling orchestrations from entities is not supported in fake tests.");
}
