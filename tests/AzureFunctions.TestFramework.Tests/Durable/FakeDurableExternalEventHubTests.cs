using AzureFunctions.TestFramework.Durable;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Durable;

/// <summary>
/// Unit tests for <see cref="FakeDurableExternalEventHub"/>.
/// </summary>
public class FakeDurableExternalEventHubTests
{
    [Fact]
    public async Task RaiseEvent_BeforeWait_WaitReturnsPayload()
    {
        var hub = new FakeDurableExternalEventHub();
        await hub.RaiseEventAsync("instance-1", "ApprovalReceived", "approved", CancellationToken.None);

        var result = await hub.WaitForEventAsync("instance-1", "ApprovalReceived", CancellationToken.None);

        Assert.Equal("approved", result);
    }

    [Fact]
    public async Task WaitForEvent_AfterRaise_ReturnsPayload()
    {
        var hub = new FakeDurableExternalEventHub();

        var waitTask = hub.WaitForEventAsync("instance-2", "OrderPlaced", CancellationToken.None);

        await Task.Delay(10);
        await hub.RaiseEventAsync("instance-2", "OrderPlaced", new { amount = 100 }, CancellationToken.None);

        var result = await waitTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(result);
    }

    [Fact]
    public async Task RaiseEvent_NullPayload_WaitReturnsNull()
    {
        var hub = new FakeDurableExternalEventHub();
        await hub.RaiseEventAsync("instance-3", "NullEvent", null, CancellationToken.None);

        var result = await hub.WaitForEventAsync("instance-3", "NullEvent", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task MultipleBufferedEvents_DequeueInOrder()
    {
        var hub = new FakeDurableExternalEventHub();
        await hub.RaiseEventAsync("instance-4", "StepComplete", "step1", CancellationToken.None);
        await hub.RaiseEventAsync("instance-4", "StepComplete", "step2", CancellationToken.None);

        var first = await hub.WaitForEventAsync("instance-4", "StepComplete", CancellationToken.None);
        var second = await hub.WaitForEventAsync("instance-4", "StepComplete", CancellationToken.None);

        Assert.Equal("step1", first);
        Assert.Equal("step2", second);
    }

    [Fact]
    public async Task WaitForEvent_CancelledBeforeRaise_ThrowsOperationCancelled()
    {
        var hub = new FakeDurableExternalEventHub();
        using var cts = new CancellationTokenSource();

        var waitTask = hub.WaitForEventAsync("instance-5", "NeverRaised", cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waitTask);
    }

    [Fact]
    public async Task RaiseEvent_CancelledToken_ThrowsOperationCancelled()
    {
        var hub = new FakeDurableExternalEventHub();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            hub.RaiseEventAsync("instance-6", "Event", "payload", cts.Token));
    }

    [Fact]
    public async Task DifferentInstances_DoNotInterfere()
    {
        var hub = new FakeDurableExternalEventHub();
        await hub.RaiseEventAsync("instance-A", "Ping", "A-payload", CancellationToken.None);
        await hub.RaiseEventAsync("instance-B", "Ping", "B-payload", CancellationToken.None);

        var a = await hub.WaitForEventAsync("instance-A", "Ping", CancellationToken.None);
        var b = await hub.WaitForEventAsync("instance-B", "Ping", CancellationToken.None);

        Assert.Equal("A-payload", a);
        Assert.Equal("B-payload", b);
    }

    [Fact]
    public async Task DifferentEventNames_SameInstance_DoNotInterfere()
    {
        var hub = new FakeDurableExternalEventHub();
        await hub.RaiseEventAsync("instance-C", "EventX", "x-payload", CancellationToken.None);
        await hub.RaiseEventAsync("instance-C", "EventY", "y-payload", CancellationToken.None);

        var x = await hub.WaitForEventAsync("instance-C", "EventX", CancellationToken.None);
        var y = await hub.WaitForEventAsync("instance-C", "EventY", CancellationToken.None);

        Assert.Equal("x-payload", x);
        Assert.Equal("y-payload", y);
    }

    [Fact]
    public void WaitForEvent_TwoWaitersOnSameKey_ThrowsInvalidOperation()
    {
        var hub = new FakeDurableExternalEventHub();
        using var cts = new CancellationTokenSource();

        // First waiter — blocks but does not complete yet
        var first = hub.WaitForEventAsync("instance-D", "Conflict", cts.Token);

        // Second waiter on same key should throw immediately
        var ex = Assert.Throws<InvalidOperationException>(() =>
            hub.WaitForEventAsync("instance-D", "Conflict", cts.Token).GetAwaiter().GetResult());

        Assert.Contains("instance-D", ex.Message);
        cts.Cancel();
    }

    [Fact]
    public void WaitForEvent_NullInstanceId_Throws()
    {
        var hub = new FakeDurableExternalEventHub();
        Assert.ThrowsAny<ArgumentException>(() =>
            hub.WaitForEventAsync(null!, "Event", CancellationToken.None).GetAwaiter().GetResult());
    }

    [Fact]
    public void WaitForEvent_EmptyInstanceId_Throws()
    {
        var hub = new FakeDurableExternalEventHub();
        Assert.ThrowsAny<ArgumentException>(() =>
            hub.WaitForEventAsync("", "Event", CancellationToken.None).GetAwaiter().GetResult());
    }

    [Fact]
    public void WaitForEvent_WhitespaceEventName_Throws()
    {
        var hub = new FakeDurableExternalEventHub();
        Assert.ThrowsAny<ArgumentException>(() =>
            hub.WaitForEventAsync("id", "   ", CancellationToken.None).GetAwaiter().GetResult());
    }

    [Fact]
    public void RaiseEvent_NullInstanceId_Throws()
    {
        var hub = new FakeDurableExternalEventHub();
        Assert.ThrowsAny<ArgumentException>(() =>
            hub.RaiseEventAsync(null!, "Event", null, CancellationToken.None).GetAwaiter().GetResult());
    }
}
