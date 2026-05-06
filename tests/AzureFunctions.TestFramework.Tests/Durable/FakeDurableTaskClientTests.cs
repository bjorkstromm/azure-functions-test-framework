using AzureFunctions.TestFramework.Durable;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Durable;

/// <summary>
/// Unit tests for <see cref="FakeDurableTaskClient"/>, covering the not-supported
/// <see cref="FakeDurableTaskClient.GetAllInstancesAsync"/> method, the null-return
/// for missing instances, and the <see cref="InvalidOperationException"/> thrown when
/// operations target an instance that has not been scheduled.
/// </summary>
public class FakeDurableTaskClientTests
{
    // ── GetAllInstancesAsync (not supported) ──────────────────────────────────

    [Fact]
    public void GetAllInstancesAsync_ThrowsNotSupportedException()
    {
        using var resources = CreateResources();

        Assert.Throws<NotSupportedException>(() =>
            resources.Client.GetAllInstancesAsync());
    }

    // ── GetInstancesAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetInstancesAsync_NonExistentInstance_ReturnsNull()
    {
        using var resources = CreateResources();

#pragma warning disable xUnit1051
        var metadata = await resources.Client.GetInstancesAsync(
            "does-not-exist",
            getInputsAndOutputs: false,
            CancellationToken.None);
#pragma warning restore xUnit1051

        Assert.Null(metadata);
    }

    [Fact]
    public async Task GetInstancesAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        using var resources = CreateResources();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            resources.Client.GetInstancesAsync("any-id", false, cts.Token));
    }

    // ── Operations on non-existent instances ─────────────────────────────────

    [Fact]
    public async Task RaiseEventAsync_NonExistentInstance_ThrowsInvalidOperationException()
    {
        using var resources = CreateResources();

#pragma warning disable xUnit1051
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resources.Client.RaiseEventAsync("missing", "event", null, CancellationToken.None));
#pragma warning restore xUnit1051

        Assert.Contains("missing", ex.Message);
    }

    [Fact]
    public async Task SuspendInstanceAsync_NonExistentInstance_ThrowsInvalidOperationException()
    {
        using var resources = CreateResources();

#pragma warning disable xUnit1051
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resources.Client.SuspendInstanceAsync("missing", reason: null, CancellationToken.None));
#pragma warning restore xUnit1051
    }

    [Fact]
    public async Task ResumeInstanceAsync_NonExistentInstance_ThrowsInvalidOperationException()
    {
        using var resources = CreateResources();

#pragma warning disable xUnit1051
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resources.Client.ResumeInstanceAsync("missing", reason: null, CancellationToken.None));
#pragma warning restore xUnit1051
    }

    [Fact]
    public async Task TerminateInstanceAsync_NonExistentInstance_ThrowsInvalidOperationException()
    {
        using var resources = CreateResources();

#pragma warning disable xUnit1051
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resources.Client.TerminateInstanceAsync("missing", options: null, CancellationToken.None));
#pragma warning restore xUnit1051
    }

    [Fact]
    public async Task WaitForInstanceCompletionAsync_NonExistentInstance_ThrowsInvalidOperationException()
    {
        using var resources = CreateResources();

#pragma warning disable xUnit1051
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resources.Client.WaitForInstanceCompletionAsync("missing"));
#pragma warning restore xUnit1051
    }

    [Fact]
    public async Task WaitForInstanceStartAsync_NonExistentInstance_ThrowsInvalidOperationException()
    {
        using var resources = CreateResources();

#pragma warning disable xUnit1051
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resources.Client.WaitForInstanceStartAsync("missing"));
#pragma warning restore xUnit1051
    }

    // ── PurgeInstanceAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task PurgeInstanceAsync_NonExistentInstance_ReturnsZeroPurgedCount()
    {
        using var resources = CreateResources();

#pragma warning disable xUnit1051
        var result = await resources.Client.PurgeInstanceAsync("does-not-exist", options: null, CancellationToken.None);
#pragma warning restore xUnit1051

        Assert.Equal(0, result.PurgedInstanceCount);
    }

    // ── ScheduleNewOrchestrationInstanceAsync ─────────────────────────────────

    [Fact]
    public async Task ScheduleNewOrchestrationInstanceAsync_CustomInstanceId_ReturnsIt()
    {
        using var resources = CreateResources();
        const string instanceId = "custom-id-001";

#pragma warning disable xUnit1051
        var returned = await resources.Client.ScheduleNewOrchestrationInstanceAsync(
            "AnyOrchestrator",
            options: new StartOrchestrationOptions { InstanceId = instanceId },
            cancellation: CancellationToken.None);
#pragma warning restore xUnit1051

        Assert.Equal(instanceId, returned);
    }

    [Fact]
    public async Task ScheduleNewOrchestrationInstanceAsync_NoInstanceId_ReturnsGeneratedId()
    {
        using var resources = CreateResources();

#pragma warning disable xUnit1051
        var returned = await resources.Client.ScheduleNewOrchestrationInstanceAsync(
            "AnyOrchestrator",
            cancellation: CancellationToken.None);
#pragma warning restore xUnit1051

        Assert.False(string.IsNullOrEmpty(returned));
    }

    [Fact]
    public async Task ScheduleNewOrchestrationInstanceAsync_AlreadyRunning_ThrowsInvalidOperation()
    {
        using var resources = CreateResources();
        const string instanceId = "duplicate-id";

#pragma warning disable xUnit1051
        await resources.Client.ScheduleNewOrchestrationInstanceAsync(
            "First",
            options: new StartOrchestrationOptions { InstanceId = instanceId },
            cancellation: CancellationToken.None);

        // Scheduling a second orchestration with the same ID while the first is still
        // Running (or Pending) should throw.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resources.Client.ScheduleNewOrchestrationInstanceAsync(
                "Second",
                options: new StartOrchestrationOptions { InstanceId = instanceId },
                cancellation: CancellationToken.None));
#pragma warning restore xUnit1051
    }

    [Fact]
    public async Task ScheduleNewOrchestrationInstanceAsync_CancelledToken_ThrowsOperationCanceled()
    {
        using var resources = CreateResources();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            resources.Client.ScheduleNewOrchestrationInstanceAsync(
                "AnyOrchestrator",
                cancellation: cts.Token));
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    private static TestResources CreateResources()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        var catalog = new FakeDurableFunctionCatalog(typeof(FakeDurableTaskClientTests).Assembly);
        var externalEventHub = new FakeDurableExternalEventHub();
        var entityRunnerLogger = services.GetRequiredService<ILogger<FakeDurableEntityRunner>>();
        var entityRunner = new FakeDurableEntityRunner(catalog, services, entityRunnerLogger);
        var runnerLogger = services.GetRequiredService<ILogger<FakeDurableOrchestrationRunner>>();
        var runner = new FakeDurableOrchestrationRunner(
            catalog, externalEventHub, services, runnerLogger, entityRunner);
        var entityClient = new FakeDurableEntityClient(entityRunner);
        var clientLogger = services.GetRequiredService<ILogger<FakeDurableTaskClient>>();
        var client = new FakeDurableTaskClient(runner, externalEventHub, entityClient, clientLogger);

        return new TestResources(client, entityRunner);
    }

    private sealed class TestResources : IDisposable
    {
        public TestResources(FakeDurableTaskClient client, FakeDurableEntityRunner entityRunner)
        {
            Client = client;
            EntityRunner = entityRunner;
        }

        public FakeDurableTaskClient Client { get; }
        private FakeDurableEntityRunner EntityRunner { get; }

        public void Dispose() => EntityRunner.Dispose();
    }
}
