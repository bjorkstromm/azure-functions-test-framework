using System.Collections.Concurrent;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.Entities;
using Microsoft.DurableTask.Converters;
using Microsoft.Extensions.Logging;

namespace AzureFunctions.TestFramework.Durable;

internal sealed class FakeDurableTaskClient : DurableTaskClient
{
    private readonly JsonDataConverter _dataConverter = JsonDataConverter.Default;
    private readonly FakeDurableExternalEventHub _externalEventHub;
    private readonly ConcurrentDictionary<string, FakeDurableInstanceState> _instances = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<FakeDurableTaskClient> _logger;
    private readonly FakeDurableOrchestrationRunner _runner;

    public FakeDurableTaskClient(
        FakeDurableOrchestrationRunner runner,
        FakeDurableExternalEventHub externalEventHub,
        FakeDurableEntityClient entityClient,
        ILogger<FakeDurableTaskClient> logger)
        : base("FAKE")
    {
        _runner = runner;
        _externalEventHub = externalEventHub;
        Entities = entityClient;
        _logger = logger;
    }

    public override DurableEntityClient Entities { get; }

    public override ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public override AsyncPageable<OrchestrationMetadata> GetAllInstancesAsync(OrchestrationQuery? filter = null)
    {
        throw new NotSupportedException("Querying all orchestration instances is not supported by the fake durable client.");
    }

    public override Task<OrchestrationMetadata?> GetInstancesAsync(
        string instanceId,
        bool getInputsAndOutputs = false,
        CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();

        return Task.FromResult(
            _instances.TryGetValue(instanceId, out var state)
                ? state.CreateMetadata(_dataConverter, getInputsAndOutputs)
                : null);
    }

    public override Task RaiseEventAsync(
        string instanceId,
        string eventName,
        object? eventPayload = null,
        CancellationToken cancellation = default)
    {
        _ = GetRequiredInstance(instanceId);
        _logger.LogInformation(
            "Raising fake durable external event {EventName} for instance {InstanceId}",
            eventName,
            instanceId);
        return _externalEventHub.RaiseEventAsync(instanceId, eventName, eventPayload, cancellation);
    }

    public override Task ResumeInstanceAsync(
        string instanceId,
        string? reason = null,
        CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var state = GetRequiredInstance(instanceId);
        state.MarkRunning();
        _logger.LogInformation("Fake durable instance {InstanceId} resumed", instanceId);
        return Task.CompletedTask;
    }

    public override Task<string> ScheduleNewOrchestrationInstanceAsync(
        TaskName orchestratorName,
        object? input = null,
        StartOrchestrationOptions? options = null,
        CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();

        var instanceId = options?.InstanceId ?? Guid.NewGuid().ToString("N");
        _logger.LogInformation("Scheduling fake durable orchestrator {OrchestratorName} as instance {InstanceId}", orchestratorName.Name, instanceId);
        var state = new FakeDurableInstanceState(orchestratorName.Name, instanceId, input);

        // Allow re-scheduling over a terminal instance (matches real Azure backend behaviour).
        _instances.AddOrUpdate(
            instanceId,
            addValue: state,
            updateValueFactory: (id, existing) =>
            {
                if (existing.RuntimeStatus is
                    OrchestrationRuntimeStatus.Completed or
                    OrchestrationRuntimeStatus.Failed or
                    OrchestrationRuntimeStatus.Terminated)
                {
                    return state;
                }

                throw new InvalidOperationException(
                    $"A fake durable orchestration with instance ID '{id}' already exists.");
            });

        state.MarkRunning();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellation, state.TerminationToken);
        state.Execution = Task.Run(async () =>
        {
            try
            {
                var result = await _runner.RunOrchestrationWithDetailsAsync(
                        orchestratorName.Name,
                        instanceId,
                        input,
                        state.MarkCustomStatus,
                        linkedCts.Token)
                    .ConfigureAwait(false);
                state.MarkCompleted(result.Output, result.CustomStatus);
                _logger.LogInformation("Fake durable instance {InstanceId} completed successfully", instanceId);
            }
            catch (OperationCanceledException) when (state.IsTerminated)
            {
                // Termination was requested — status already set to Terminated.
                _logger.LogInformation("Fake durable instance {InstanceId} was terminated", instanceId);
            }
            catch (Exception exception)
            {
                state.MarkFailed(exception);
                _logger.LogWarning(exception, "Fake durable instance {InstanceId} failed", instanceId);
            }
            finally
            {
                linkedCts.Dispose();
            }
        }, cancellation);

        return Task.FromResult(instanceId);
    }

    public override Task SuspendInstanceAsync(
        string instanceId,
        string? reason = null,
        CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var state = GetRequiredInstance(instanceId);
        state.MarkSuspended();
        _logger.LogInformation("Fake durable instance {InstanceId} suspended", instanceId);
        return Task.CompletedTask;
    }

    public override Task TerminateInstanceAsync(
        string instanceId,
        TerminateInstanceOptions? options = null,
        CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var state = GetRequiredInstance(instanceId);
        state.MarkTerminated();
        _logger.LogInformation("Fake durable instance {InstanceId} terminated", instanceId);
        return Task.CompletedTask;
    }

    public override async Task<OrchestrationMetadata> WaitForInstanceCompletionAsync(
        string instanceId,
        bool getInputsAndOutputs = false,
        CancellationToken cancellation = default)
    {
        var state = GetRequiredInstance(instanceId);
        await state.Execution.WaitAsync(cancellation).ConfigureAwait(false);
        return state.CreateMetadata(_dataConverter, getInputsAndOutputs);
    }

    public override async Task<OrchestrationMetadata> WaitForInstanceStartAsync(
        string instanceId,
        bool getInputsAndOutputs = false,
        CancellationToken cancellation = default)
    {
        var state = GetRequiredInstance(instanceId);
        await Task.Yield();
        cancellation.ThrowIfCancellationRequested();
        return state.CreateMetadata(_dataConverter, getInputsAndOutputs);
    }

    public override Task<PurgeResult> PurgeInstanceAsync(
        string instanceId,
        PurgeInstanceOptions? options = null,
        CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var removed = _instances.TryRemove(instanceId, out _);
        _logger.LogInformation("Fake durable instance {InstanceId} purged (found={Found})", instanceId, removed);
        return Task.FromResult(new PurgeResult(removed ? 1 : 0));
    }

    private FakeDurableInstanceState GetRequiredInstance(string instanceId)
    {
        if (_instances.TryGetValue(instanceId, out var state))
        {
            return state;
        }

        throw new InvalidOperationException($"A fake durable orchestration with instance ID '{instanceId}' does not exist.");
    }

    private sealed class FakeDurableInstanceState : IDisposable
    {
        private readonly object _syncLock = new();
        private readonly CancellationTokenSource _terminationCts = new();
        private object? _customStatus;
        private Exception? _exception;
        private object? _output;

        public FakeDurableInstanceState(string orchestratorName, string instanceId, object? input)
        {
            OrchestratorName = orchestratorName;
            InstanceId = instanceId;
            Input = input;
            CreatedAt = DateTimeOffset.UtcNow;
            LastUpdatedAt = CreatedAt;
            RuntimeStatus = OrchestrationRuntimeStatus.Pending;
            Execution = Task.CompletedTask;
        }

        public DateTimeOffset CreatedAt { get; }

        public Task Execution { get; set; }

        public object? Input { get; }

        public string InstanceId { get; }

        public bool IsTerminated { get; private set; }

        public DateTimeOffset LastUpdatedAt { get; private set; }

        public string OrchestratorName { get; }

        public OrchestrationRuntimeStatus RuntimeStatus { get; private set; }

        public CancellationToken TerminationToken => _terminationCts.Token;

        public void Dispose() => _terminationCts.Dispose();

        public void MarkCompleted(object? output, object? customStatus)
        {
            lock (_syncLock)
            {
                _output = output;
                _customStatus = customStatus;
                RuntimeStatus = OrchestrationRuntimeStatus.Completed;
                LastUpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        public void MarkCustomStatus(object? customStatus)
        {
            lock (_syncLock)
            {
                _customStatus = customStatus;
                LastUpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        public void MarkFailed(Exception exception)
        {
            lock (_syncLock)
            {
                _exception = exception;
                RuntimeStatus = OrchestrationRuntimeStatus.Failed;
                LastUpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        public void MarkRunning()
        {
            lock (_syncLock)
            {
                RuntimeStatus = OrchestrationRuntimeStatus.Running;
                LastUpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        public void MarkSuspended()
        {
            lock (_syncLock)
            {
                RuntimeStatus = OrchestrationRuntimeStatus.Suspended;
                LastUpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        public void MarkTerminated()
        {
            lock (_syncLock)
            {
                IsTerminated = true;
                RuntimeStatus = OrchestrationRuntimeStatus.Terminated;
                LastUpdatedAt = DateTimeOffset.UtcNow;
            }

            _terminationCts.Cancel();
        }

        public OrchestrationMetadata CreateMetadata(JsonDataConverter dataConverter, bool getInputsAndOutputs)
        {
            lock (_syncLock)
            {
                return new OrchestrationMetadata(OrchestratorName, InstanceId)
                {
                    CreatedAt = CreatedAt,
                    LastUpdatedAt = LastUpdatedAt,
                    RuntimeStatus = RuntimeStatus,
                    DataConverter = getInputsAndOutputs ? dataConverter : null,
                    SerializedInput = getInputsAndOutputs ? dataConverter.Serialize(Input) : null,
                    SerializedOutput = getInputsAndOutputs ? dataConverter.Serialize(_output) : null,
                    SerializedCustomStatus = getInputsAndOutputs ? dataConverter.Serialize(_customStatus) : null,
                    FailureDetails = _exception is null ? null : TaskFailureDetails.FromException(_exception)
                };
            }
        }
    }
}
