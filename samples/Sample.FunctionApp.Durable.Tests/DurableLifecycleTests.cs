using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Durable;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Sample.FunctionApp.Durable.Tests;

/// <summary>
/// Tests that verify <see cref="FakeDurableTaskClient"/> correctly implements the instance
/// lifecycle operations added by the fix: re-scheduling over terminal instances,
/// suspend/resume, terminate, and purge.
/// </summary>
public sealed class DurableLifecycleTests
{
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    private readonly ITestOutputHelper _output;

    public DurableLifecycleTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ── Re-scheduling ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ScheduleAsync_AllowsReschedulingOverCompletedInstance()
    {
        // Arrange
        await using var host = await CreateHostAsync();
        var client = GetDurableClient(host);
        const string instanceId = "reschedule-over-completed";

#pragma warning disable xUnit1051
        var firstId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingOrchestration),
            "martin",
            new StartOrchestrationOptions { InstanceId = instanceId });

        await client.WaitForInstanceCompletionAsync(firstId, getInputsAndOutputs: false);
#pragma warning restore xUnit1051

        var completedStatus = await client.GetInstancesAsync(instanceId, false, TestCancellation);
        Assert.Equal(OrchestrationRuntimeStatus.Completed, completedStatus?.RuntimeStatus);

        // Act — re-schedule with the same instance ID after completion
#pragma warning disable xUnit1051
        var secondId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingOrchestration),
            "world",
            new StartOrchestrationOptions { InstanceId = instanceId });

        var meta = await client.WaitForInstanceCompletionAsync(secondId, getInputsAndOutputs: true);
#pragma warning restore xUnit1051

        // Assert
        Assert.Equal(instanceId, secondId);
        Assert.Equal(OrchestrationRuntimeStatus.Completed, meta.RuntimeStatus);
        Assert.Equal("Hello, world!", meta.ReadOutputAs<string>());
    }

    [Fact]
    public async Task ScheduleAsync_AllowsReschedulingOverTerminatedInstance()
    {
        // Arrange — start an orchestration that waits for an event, then terminate it.
        await using var host = await CreateHostAsync();
        var client = GetDurableClient(host);
        const string instanceId = "reschedule-over-terminated";

#pragma warning disable xUnit1051
        await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingAwaitEventOrchestration),
            "martin",
            new StartOrchestrationOptions { InstanceId = instanceId });
#pragma warning restore xUnit1051

        // Wait until the orchestration is blocked on the external event.
        await WaitForStatusAsync(client, instanceId, OrchestrationRuntimeStatus.Running);

        // Terminate the running instance.
        await client.TerminateInstanceAsync(
            instanceId,
            null,
            TestCancellation);

        // Confirm termination.
#pragma warning disable xUnit1051
        await client.WaitForInstanceCompletionAsync(instanceId);
#pragma warning restore xUnit1051
        var terminatedMeta = await client.GetInstancesAsync(instanceId, false, TestCancellation);
        Assert.Equal(OrchestrationRuntimeStatus.Terminated, terminatedMeta?.RuntimeStatus);

        // Act — re-schedule over the terminated instance.
#pragma warning disable xUnit1051
        var newId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingOrchestration),
            "world",
            new StartOrchestrationOptions { InstanceId = instanceId });

        var meta = await client.WaitForInstanceCompletionAsync(newId, getInputsAndOutputs: true);
#pragma warning restore xUnit1051

        // Assert
        Assert.Equal(instanceId, newId);
        Assert.Equal(OrchestrationRuntimeStatus.Completed, meta.RuntimeStatus);
        Assert.Equal("Hello, world!", meta.ReadOutputAs<string>());
    }

    [Fact]
    public async Task ScheduleAsync_ThrowsWhenInstanceIsAlreadyRunning()
    {
        // Arrange — start an orchestration that waits for an event.
        await using var host = await CreateHostAsync();
        var client = GetDurableClient(host);
        const string instanceId = "cannot-reschedule-running";

#pragma warning disable xUnit1051
        await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingAwaitEventOrchestration),
            "martin",
            new StartOrchestrationOptions { InstanceId = instanceId });
#pragma warning restore xUnit1051

        await WaitForStatusAsync(client, instanceId, OrchestrationRuntimeStatus.Running);

        // Act & Assert — re-scheduling a running instance must throw.
#pragma warning disable xUnit1051
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ScheduleNewOrchestrationInstanceAsync(
                nameof(DurableGreetingFunctions.RunGreetingOrchestration),
                "world",
                new StartOrchestrationOptions { InstanceId = instanceId }));
#pragma warning restore xUnit1051

        Assert.Contains(instanceId, ex.Message);

        // Clean up — terminate the waiting instance so the host can shut down cleanly.
        await client.TerminateInstanceAsync(instanceId, cancellation: TestCancellation);
#pragma warning disable xUnit1051
        await client.WaitForInstanceCompletionAsync(instanceId);
#pragma warning restore xUnit1051
    }

    // ── Suspend / Resume ──────────────────────────────────────────────────────

    [Fact]
    public async Task SuspendInstance_ChangesStatusToSuspended()
    {
        // Arrange
        await using var host = await CreateHostAsync();
        var client = GetDurableClient(host);
        const string instanceId = "suspend-test";

#pragma warning disable xUnit1051
        await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingAwaitEventOrchestration),
            "martin",
            new StartOrchestrationOptions { InstanceId = instanceId });
#pragma warning restore xUnit1051

        await WaitForStatusAsync(client, instanceId, OrchestrationRuntimeStatus.Running);

        // Act
        await client.SuspendInstanceAsync(instanceId, "test suspension", TestCancellation);

        // Assert
        var meta = await client.GetInstancesAsync(instanceId, false, TestCancellation);
        Assert.Equal(OrchestrationRuntimeStatus.Suspended, meta?.RuntimeStatus);

        // Clean up
        await client.TerminateInstanceAsync(instanceId, null, TestCancellation);
#pragma warning disable xUnit1051
        await client.WaitForInstanceCompletionAsync(instanceId);
#pragma warning restore xUnit1051
    }

    [Fact]
    public async Task ResumeInstance_ChangesStatusBackToRunning()
    {
        // Arrange
        await using var host = await CreateHostAsync();
        var client = GetDurableClient(host);
        const string instanceId = "resume-test";

#pragma warning disable xUnit1051
        await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingAwaitEventOrchestration),
            "martin",
            new StartOrchestrationOptions { InstanceId = instanceId });
#pragma warning restore xUnit1051

        await WaitForStatusAsync(client, instanceId, OrchestrationRuntimeStatus.Running);
        await client.SuspendInstanceAsync(instanceId, reason: null, TestCancellation);

        var suspendedMeta = await client.GetInstancesAsync(instanceId, false, TestCancellation);
        Assert.Equal(OrchestrationRuntimeStatus.Suspended, suspendedMeta?.RuntimeStatus);

        // Act
        await client.ResumeInstanceAsync(instanceId, reason: null, TestCancellation);

        // Assert
        var resumedMeta = await client.GetInstancesAsync(instanceId, false, TestCancellation);
        Assert.Equal(OrchestrationRuntimeStatus.Running, resumedMeta?.RuntimeStatus);

        // Clean up
        await client.TerminateInstanceAsync(instanceId, cancellation: TestCancellation);
#pragma warning disable xUnit1051
        await client.WaitForInstanceCompletionAsync(instanceId);
#pragma warning restore xUnit1051
    }

    // ── Terminate ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task TerminateInstance_ChangesStatusToTerminated()
    {
        // Arrange
        await using var host = await CreateHostAsync();
        var client = GetDurableClient(host);
        const string instanceId = "terminate-test";

#pragma warning disable xUnit1051
        await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingAwaitEventOrchestration),
            "martin",
            new StartOrchestrationOptions { InstanceId = instanceId });
#pragma warning restore xUnit1051

        await WaitForStatusAsync(client, instanceId, OrchestrationRuntimeStatus.Running);

        // Act
        await client.TerminateInstanceAsync(
            instanceId,
            null,
            TestCancellation);

        // Assert — wait for the background Task.Run to finish
#pragma warning disable xUnit1051
        var meta = await client.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: false);
#pragma warning restore xUnit1051
        Assert.Equal(OrchestrationRuntimeStatus.Terminated, meta.RuntimeStatus);
    }

    // ── Purge ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PurgeInstance_RemovesExistingInstance()
    {
        // Arrange
        await using var host = await CreateHostAsync();
        var client = GetDurableClient(host);
        const string instanceId = "purge-test";

#pragma warning disable xUnit1051
        await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingOrchestration),
            "martin",
            new StartOrchestrationOptions { InstanceId = instanceId });

        await client.WaitForInstanceCompletionAsync(instanceId);
#pragma warning restore xUnit1051

        // Pre-condition: instance exists.
        var before = await client.GetInstancesAsync(instanceId, false, TestCancellation);
        Assert.NotNull(before);

        // Act
        var result = await client.PurgeInstanceAsync(instanceId, null, TestCancellation);

        // Assert
        Assert.Equal(1, result.PurgedInstanceCount);
        var after = await client.GetInstancesAsync(instanceId, false, TestCancellation);
        Assert.Null(after);
    }

    [Fact]
    public async Task PurgeInstance_ReturnsZeroForNonExistentInstance()
    {
        await using var host = await CreateHostAsync();
        var client = GetDurableClient(host);

        var result = await client.PurgeInstanceAsync("does-not-exist", null, TestCancellation);

        Assert.Equal(0, result.PurgedInstanceCount);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Task<IFunctionsTestHost> CreateHostAsync()
    {
        return new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(DurableGreetingFunctions).Assembly)
            .WithLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new XUnitLoggerProvider(_output))))
            .WithHostBuilderFactory(args => new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureServices(s => s.AddSingleton<GreetingFormatter>()))
            .ConfigureFakeDurableSupport(typeof(DurableGreetingFunctions).Assembly)
            .BuildAndStartAsync(TestCancellation);
    }

    private static DurableTaskClient GetDurableClient(IFunctionsTestHost host) =>
        host.Services.GetRequiredService<FunctionsDurableClientProvider>().GetClient();

    private static async Task WaitForStatusAsync(
        DurableTaskClient client,
        string instanceId,
        OrchestrationRuntimeStatus expectedStatus)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken, timeout.Token);

        while (true)
        {
            var meta = await client.GetInstancesAsync(instanceId, false, linked.Token);
            if (meta?.RuntimeStatus == expectedStatus)
            {
                return;
            }

            await Task.Delay(25, linked.Token);
        }
    }
}
