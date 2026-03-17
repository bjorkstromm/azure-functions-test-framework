using System.Collections.Concurrent;

namespace AzureFunctions.TestFramework.Durable;

internal sealed class FakeDurableExternalEventHub
{
    private readonly ConcurrentDictionary<EventKey, EventState> _states = new();

    public async Task<object?> WaitForEventAsync(
        string instanceId,
        string eventName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

        var key = new EventKey(instanceId, eventName);
        var state = _states.GetOrAdd(key, static _ => new EventState());
        TaskCompletionSource<object?>? waiter = null;

        lock (state.SyncRoot)
        {
            if (state.BufferedEvents.Count > 0)
            {
                var payload = state.BufferedEvents.Dequeue();
                TryCleanup(key, state);
                return payload;
            }

            if (state.Waiter is not null)
            {
                throw new InvalidOperationException(
                    $"A fake durable orchestration waiter is already registered for instance '{instanceId}' and event '{eventName}'.");
            }

            waiter = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            state.Waiter = waiter;
        }

        using var registration = cancellationToken.Register(() =>
        {
            lock (state.SyncRoot)
            {
                if (ReferenceEquals(state.Waiter, waiter))
                {
                    state.Waiter = null;
                    waiter!.TrySetCanceled(cancellationToken);
                    TryCleanup(key, state);
                }
            }
        });

        try
        {
            return await waiter!.Task.ConfigureAwait(false);
        }
        finally
        {
            lock (state.SyncRoot)
            {
                if (ReferenceEquals(state.Waiter, waiter))
                {
                    state.Waiter = null;
                }

                TryCleanup(key, state);
            }
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
        var state = _states.GetOrAdd(key, static _ => new EventState());
        TaskCompletionSource<object?>? waiter = null;

        lock (state.SyncRoot)
        {
            if (state.Waiter is not null)
            {
                waiter = state.Waiter;
                state.Waiter = null;
            }
            else
            {
                state.BufferedEvents.Enqueue(payload);
            }

            TryCleanup(key, state);
        }

        waiter?.TrySetResult(payload);
        return Task.CompletedTask;
    }

    private void TryCleanup(EventKey key, EventState state)
    {
        if (state.Waiter is null && state.BufferedEvents.Count == 0)
        {
            _states.TryRemove(new KeyValuePair<EventKey, EventState>(key, state));
        }
    }

    private sealed class EventState
    {
        public Queue<object?> BufferedEvents { get; } = new();

        public object SyncRoot { get; } = new();

        public TaskCompletionSource<object?>? Waiter { get; set; }
    }

    private readonly record struct EventKey(string InstanceId, string EventName);
}
