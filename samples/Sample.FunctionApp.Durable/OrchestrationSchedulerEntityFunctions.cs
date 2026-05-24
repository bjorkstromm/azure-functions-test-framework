using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;

namespace Sample.FunctionApp.Durable;

/// <summary>
/// Represents this type.
/// </summary>
public sealed class OrchestrationSchedulerEntity : ITaskEntity
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Function(nameof(OrchestrationSchedulerEntity))]
    public Task Run([EntityTrigger] TaskEntityDispatcher dispatcher) => dispatcher.DispatchAsync<OrchestrationSchedulerEntity>();

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public ValueTask<object?> RunAsync(TaskEntityOperation operation)
    {
        return operation.Name.ToLowerInvariant() switch
        {
            "schedule" => ScheduleAsync(operation),
            _ => throw new InvalidOperationException($"Unknown operation '{operation.Name}'.")
        };
    }

    private static ValueTask<object?> ScheduleAsync(TaskEntityOperation operation)
    {
        var name = operation.GetInput<string>() ?? "world";
        var instanceId = operation.Context.ScheduleNewOrchestration(
            new TaskName(nameof(DurableGreetingFunctions.RunGreetingOrchestration)),
            name,
            options: null);
        return ValueTask.FromResult<object?>(instanceId);
    }
}
