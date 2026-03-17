using System.Collections.Concurrent;

namespace AzureFunctions.TestFramework.Durable;

internal sealed class FakeDurableExternalEventHub
{
    private readonly ConcurrentDictionary<EventKey, TaskCompletionSource<object?>> _waiters = new();

    public async Task<object?> WaitForEventAsync(
        string instanceId,
        string eventName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

        var key = new EventKey(instanceId, eventName);
        var waiter = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_waiters.TryAdd(key, waiter))
        {
            throw new InvalidOperationException(
                $"A fake durable orchestration waiter is already registered for instance '{instanceId}' and event '{eventName}'.");
        }

        using var registration = cancellationToken.Register(() =>
        {
            if (_waiters.TryRemove(key, out var pendingWaiter))
            {
                pendingWaiter.TrySetCanceled(cancellationToken);
            }
        });

        try
        {
            return await waiter.Task.ConfigureAwait(false);
        }
        finally
        {
            _waiters.TryRemove(key, out _);
        }
    }

    public Task RaiseEventAsync(
        string instanceId,
        string eventName,
        object? payload,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        cancellationToken.ThrowIfCancellationRequested();

        var key = new EventKey(instanceId, eventName);
        if (!_waiters.TryRemove(key, out var waiter))
        {
            throw new InvalidOperationException(
                $"No fake durable orchestration is currently waiting for event '{eventName}' on instance '{instanceId}'.");
        }

        waiter.TrySetResult(payload);
        return Task.CompletedTask;
    }

    private readonly record struct EventKey(string InstanceId, string EventName);
}
