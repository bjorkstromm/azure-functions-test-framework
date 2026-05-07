using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;

namespace AzureFunctions.TestFramework.Durable;

internal sealed class FakeTaskEntityContext : TaskEntityContext
{
    private readonly FakeDurableEntityRunner _entityRunner;
    private readonly ILogger _logger;
    private readonly Func<string, object?, StartOrchestrationOptions?, string>? _scheduleOrchestration;

    public FakeTaskEntityContext(
        EntityInstanceId entityId,
        FakeDurableEntityRunner entityRunner,
        ILogger logger,
        Func<string, object?, StartOrchestrationOptions?, string>? scheduleOrchestration = null)
    {
        Id = entityId;
        _entityRunner = entityRunner;
        _logger = logger;
        _scheduleOrchestration = scheduleOrchestration;
    }

    public override EntityInstanceId Id { get; }

    public override void SignalEntity(EntityInstanceId id, string operationName, object? input, SignalEntityOptions? options)
    {
        // Fire-and-forget: start a background task so the calling entity is not blocked.
        // Log any failure so it is not silently swallowed in tests.
        _ = Task.Run(() => _entityRunner.SignalEntityAsync(id, operationName, input, options, CancellationToken.None))
            .ContinueWith(
                t => _logger.LogError(t.Exception, "Unhandled exception in fire-and-forget entity signal {Operation} on {EntityId}", operationName, id),
                TaskContinuationOptions.OnlyOnFaulted);
    }

    public override string ScheduleNewOrchestration(TaskName name, object? input, StartOrchestrationOptions? options)
        => _scheduleOrchestration is null
            ? throw new NotSupportedException("Scheduling orchestrations from entities is not supported in fake tests.")
            : _scheduleOrchestration(name.Name, input, options);
}
