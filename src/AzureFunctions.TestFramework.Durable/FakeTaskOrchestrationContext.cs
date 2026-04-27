using System.Text.Json;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzureFunctions.TestFramework.Durable;

internal sealed class FakeTaskOrchestrationContext : TaskOrchestrationContext
{
    private readonly Func<TaskName, object?, CancellationToken, Task<object?>> _activityDispatcher;
    private readonly Func<string, string, CancellationToken, Task<object?>> _externalEventDispatcher;
    private readonly Func<TaskName, object?, CancellationToken, Task<object?>> _subOrchestrationDispatcher;
    private readonly Action<object?>? _customStatusSink;
    private readonly CancellationToken _executionCancellationToken;
    private readonly object? _input;
    private readonly IServiceProvider _serviceProvider;

    public FakeTaskOrchestrationContext(
        string orchestrationName,
        string instanceId,
        object? input,
        IServiceProvider serviceProvider,
        Func<TaskName, object?, CancellationToken, Task<object?>> activityDispatcher,
        Func<TaskName, object?, CancellationToken, Task<object?>> subOrchestrationDispatcher,
        Func<string, string, CancellationToken, Task<object?>> externalEventDispatcher,
        Action<object?>? customStatusSink = null,
        FakeDurableEntityRunner? entityRunner = null,
        CancellationToken executionCancellationToken = default)
    {
        Name = orchestrationName;
        InstanceId = instanceId;
        _input = input;
        _serviceProvider = serviceProvider;
        _activityDispatcher = activityDispatcher;
        _subOrchestrationDispatcher = subOrchestrationDispatcher;
        _externalEventDispatcher = externalEventDispatcher;
        _customStatusSink = customStatusSink;
        _executionCancellationToken = executionCancellationToken;
        Entities = entityRunner is not null
            ? new FakeTaskOrchestrationEntityFeature(entityRunner)
            : base.Entities;
    }

    public object? ContinueAsNewInput { get; private set; }

    public object? CustomStatus { get; private set; }

    public bool IsContinueAsNew { get; private set; }

    public override DateTime CurrentUtcDateTime => DateTime.UtcNow;

    public override TaskOrchestrationEntityFeature Entities { get; }

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
        IsContinueAsNew = true;
        ContinueAsNewInput = newInput;
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
        _customStatusSink?.Invoke(customStatus);
    }

    public override async Task<T> WaitForExternalEvent<T>(string eventName, CancellationToken cancellationToken = default)
    {
        // Combine the call-site token with the instance's execution token so that termination
        // (which cancels _executionCancellationToken) always unblocks a waiting orchestration,
        // even when the orchestration itself did not pass a cancellation token.
        using var combined = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _executionCancellationToken);
        var payload = await _externalEventDispatcher(InstanceId, eventName, combined.Token).ConfigureAwait(false);
        return (T?)FakeDurableOrchestrationRunner.ConvertValue(payload, typeof(T))!;
    }
}
