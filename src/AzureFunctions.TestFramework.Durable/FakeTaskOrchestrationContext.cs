using System.Text.Json;
using Microsoft.DurableTask;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzureFunctions.TestFramework.Durable;

internal sealed class FakeTaskOrchestrationContext : TaskOrchestrationContext
{
    private readonly Func<TaskName, object?, CancellationToken, Task<object?>> _activityDispatcher;
    private readonly Func<TaskName, object?, CancellationToken, Task<object?>> _subOrchestrationDispatcher;
    private readonly object? _input;
    private readonly IServiceProvider _serviceProvider;

    public FakeTaskOrchestrationContext(
        string orchestrationName,
        string instanceId,
        object? input,
        IServiceProvider serviceProvider,
        Func<TaskName, object?, CancellationToken, Task<object?>> activityDispatcher,
        Func<TaskName, object?, CancellationToken, Task<object?>> subOrchestrationDispatcher)
    {
        Name = orchestrationName;
        InstanceId = instanceId;
        _input = input;
        _serviceProvider = serviceProvider;
        _activityDispatcher = activityDispatcher;
        _subOrchestrationDispatcher = subOrchestrationDispatcher;
    }

    public object? ContinueAsNewParameter { get; private set; }

    public object? CustomStatus { get; private set; }

    public override DateTime CurrentUtcDateTime => DateTime.UtcNow;

    public override string InstanceId { get; }

    public override bool IsReplaying => false;

    public override TaskName Name { get; }

    public override ParentOrchestrationInstance? Parent => null;

    protected override ILoggerFactory LoggerFactory => _serviceProvider.GetRequiredService<ILoggerFactory>();

    public override async Task<TResult> CallActivityAsync<TResult>(
        TaskName name,
        object? input = null,
        TaskOptions? options = null)
    {
        var result = await _activityDispatcher(name, input, CancellationToken.None).ConfigureAwait(false);
        return (TResult?)FakeDurableOrchestrationRunner.ConvertValue(result, typeof(TResult))!;
    }

    public override Task<TResult> CallSubOrchestratorAsync<TResult>(
        TaskName orchestratorName,
        object? input = null,
        TaskOptions? options = null)
    {
        return CallSubOrchestratorCoreAsync<TResult>(orchestratorName, input);
    }

    private async Task<TResult> CallSubOrchestratorCoreAsync<TResult>(TaskName orchestratorName, object? input)
    {
        var result = await _subOrchestrationDispatcher(orchestratorName, input, CancellationToken.None).ConfigureAwait(false);
        return (TResult?)FakeDurableOrchestrationRunner.ConvertValue(result, typeof(TResult))!;
    }

    public override void ContinueAsNew(object? newInput = null, bool preserveUnprocessedEvents = true)
    {
        ContinueAsNewParameter = newInput;
    }

    public override Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public override T GetInput<T>()
    {
        if (_input is null)
        {
            return default!;
        }

        if (_input is T typed)
        {
            return typed;
        }

        var json = JsonSerializer.Serialize(_input);
        return JsonSerializer.Deserialize<T>(json)!;
    }

    public override Guid NewGuid() => Guid.NewGuid();

    public override void SendEvent(string instanceId, string eventName, object payload)
    {
        throw new NotSupportedException("External events are not supported by the fake durable runner.");
    }

    public override void SetCustomStatus(object? customStatus)
    {
        CustomStatus = customStatus;
    }

    public override Task<T> WaitForExternalEvent<T>(string eventName, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("External events are not supported by the fake durable runner.");
    }
}
