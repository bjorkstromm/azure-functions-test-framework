using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Durable;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Sample.FunctionApp.Durable.Tests;

/// <summary>
/// End-to-end tests for <see cref="EternalCounterFunctions"/>.
/// Demonstrates testing an eternal orchestrator via HTTP triggers and the direct
/// <see cref="DurableTaskClient"/> — covering the full lifecycle:
/// start, increment, suspend, resume, terminate, and re-start.
/// </summary>
public sealed class DurableEternalOrchestrationTests
{
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    private readonly ITestOutputHelper _output;

    public DurableEternalOrchestrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ── HTTP-based lifecycle tests ────────────────────────────────────────────

    [Fact]
    public async Task StartEternalCounter_ReturnsAccepted_AndInstanceIsRunning()
    {
        await using var host = await CreateHostAsync();
        using var httpClient = host.CreateHttpClient();

        using var response = await httpClient.PostAsync("/api/eternal/start", null, TestCancellation);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var instanceId = await response.Content.ReadAsStringAsync(TestCancellation);
        Assert.Equal("eternal-counter-singleton", instanceId);

        // Confirm the orchestration is running via the status endpoint.
        await WaitForHttpStatusAsync(httpClient, $"/api/eternal/{instanceId}/status", "Running");
    }

    [Fact]
    public async Task StartEternalCounter_ReturnsConflict_WhenAlreadyRunning()
    {
        await using var host = await CreateHostAsync();
        using var httpClient = host.CreateHttpClient();

        // Start it once.
        using var first = await httpClient.PostAsync("/api/eternal/start", null, TestCancellation);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var instanceId = await first.Content.ReadAsStringAsync(TestCancellation);

        await WaitForHttpStatusAsync(httpClient, $"/api/eternal/{instanceId}/status", "Running");

        // Starting again should conflict.
        using var second = await httpClient.PostAsync("/api/eternal/start", null, TestCancellation);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        // Clean up
        await TerminateAndWaitAsync(host, instanceId);
    }

    [Fact]
    public async Task IncrementEternalCounter_AdvancesCount()
    {
        await using var host = await CreateHostAsync();
        using var httpClient = host.CreateHttpClient();

        // Start.
        using var startResponse = await httpClient.PostAsync("/api/eternal/start", null, TestCancellation);
        var instanceId = await startResponse.Content.ReadAsStringAsync(TestCancellation);

        await WaitForHttpStatusAsync(httpClient, $"/api/eternal/{instanceId}/status", "Running");

        // Increment twice.
        using var inc1 = await httpClient.PostAsync($"/api/eternal/{instanceId}/increment", null, TestCancellation);
        Assert.Equal(HttpStatusCode.Accepted, inc1.StatusCode);

        await WaitForCountAsync(httpClient, instanceId, expectedCount: 1);

        using var inc2 = await httpClient.PostAsync($"/api/eternal/{instanceId}/increment", null, TestCancellation);
        Assert.Equal(HttpStatusCode.Accepted, inc2.StatusCode);

        await WaitForCountAsync(httpClient, instanceId, expectedCount: 2);

        // Clean up.
        await TerminateAndWaitAsync(host, instanceId);
    }

    [Fact]
    public async Task SuspendAndResumeEternalCounter_ChangesStatusAccordingly()
    {
        await using var host = await CreateHostAsync();
        using var httpClient = host.CreateHttpClient();

        using var startResponse = await httpClient.PostAsync("/api/eternal/start", null, TestCancellation);
        var instanceId = await startResponse.Content.ReadAsStringAsync(TestCancellation);
        await WaitForHttpStatusAsync(httpClient, $"/api/eternal/{instanceId}/status", "Running");

        // Suspend.
        using var suspendResponse = await httpClient.PostAsync(
            $"/api/eternal/{instanceId}/control",
            new StringContent("{\"action\":\"suspend\"}", System.Text.Encoding.UTF8, "application/json"),
            TestCancellation);
        Assert.Equal(HttpStatusCode.OK, suspendResponse.StatusCode);
        await WaitForHttpStatusAsync(httpClient, $"/api/eternal/{instanceId}/status", "Suspended");

        // Resume.
        using var resumeResponse = await httpClient.PostAsync(
            $"/api/eternal/{instanceId}/control",
            new StringContent("{\"action\":\"resume\"}", System.Text.Encoding.UTF8, "application/json"),
            TestCancellation);
        Assert.Equal(HttpStatusCode.OK, resumeResponse.StatusCode);
        await WaitForHttpStatusAsync(httpClient, $"/api/eternal/{instanceId}/status", "Running");

        // Clean up.
        await TerminateAndWaitAsync(host, instanceId);
    }

    [Fact]
    public async Task TerminateEternalCounter_ChangesStatusToTerminated()
    {
        await using var host = await CreateHostAsync();
        using var httpClient = host.CreateHttpClient();

        using var startResponse = await httpClient.PostAsync("/api/eternal/start", null, TestCancellation);
        var instanceId = await startResponse.Content.ReadAsStringAsync(TestCancellation);
        await WaitForHttpStatusAsync(httpClient, $"/api/eternal/{instanceId}/status", "Running");

        // Terminate (force=true to avoid the running-guard conflict path).
        using var termResponse = await httpClient.PostAsync(
            $"/api/eternal/{instanceId}/control",
            new StringContent("{\"action\":\"terminate\",\"force\":true}", System.Text.Encoding.UTF8, "application/json"),
            TestCancellation);
        Assert.Equal(HttpStatusCode.OK, termResponse.StatusCode);

        await TerminateAndWaitAsync(host, instanceId);

        await WaitForHttpStatusAsync(httpClient, $"/api/eternal/{instanceId}/status", "Terminated");
    }

    [Fact]
    public async Task TerminateWithForce_WhenSuspended_Terminates()
    {
        await using var host = await CreateHostAsync();
        using var httpClient = host.CreateHttpClient();

        using var startResponse = await httpClient.PostAsync("/api/eternal/start", null, TestCancellation);
        var instanceId = await startResponse.Content.ReadAsStringAsync(TestCancellation);
        await WaitForHttpStatusAsync(httpClient, $"/api/eternal/{instanceId}/status", "Running");

        // Suspend first.
        using var suspendResponse = await httpClient.PostAsync(
            $"/api/eternal/{instanceId}/control",
            new StringContent("{\"action\":\"suspend\"}", System.Text.Encoding.UTF8, "application/json"),
            TestCancellation);
        Assert.Equal(HttpStatusCode.OK, suspendResponse.StatusCode);

        // Terminate without force should conflict (not running).
        using var noForce = await httpClient.PostAsync(
            $"/api/eternal/{instanceId}/control",
            new StringContent("{\"action\":\"terminate\"}", System.Text.Encoding.UTF8, "application/json"),
            TestCancellation);
        Assert.Equal(HttpStatusCode.Conflict, noForce.StatusCode);

        // Terminate with force should succeed.
        using var withForce = await httpClient.PostAsync(
            $"/api/eternal/{instanceId}/control",
            new StringContent("{\"action\":\"terminate\",\"force\":true}", System.Text.Encoding.UTF8, "application/json"),
            TestCancellation);
        Assert.Equal(HttpStatusCode.OK, withForce.StatusCode);

        await TerminateAndWaitAsync(host, instanceId);
        await WaitForHttpStatusAsync(httpClient, $"/api/eternal/{instanceId}/status", "Terminated");
    }

    [Fact]
    public async Task StartEternalCounter_AllowsRestartAfterTermination()
    {
        await using var host = await CreateHostAsync();
        using var httpClient = host.CreateHttpClient();

        // Start → terminate.
        using var startResponse = await httpClient.PostAsync("/api/eternal/start", null, TestCancellation);
        var instanceId = await startResponse.Content.ReadAsStringAsync(TestCancellation);
        await WaitForHttpStatusAsync(httpClient, $"/api/eternal/{instanceId}/status", "Running");

        await TerminateAndWaitAsync(host, instanceId);
        await WaitForHttpStatusAsync(httpClient, $"/api/eternal/{instanceId}/status", "Terminated");

        // Re-start should succeed (not conflict).
        using var restartResponse = await httpClient.PostAsync("/api/eternal/start", null, TestCancellation);
        Assert.Equal(HttpStatusCode.Accepted, restartResponse.StatusCode);

        await WaitForHttpStatusAsync(httpClient, $"/api/eternal/{instanceId}/status", "Running");

        // Final clean up.
        await TerminateAndWaitAsync(host, instanceId);
    }

    // ── Direct-client eternal orchestrator test ───────────────────────────────

    [Fact]
    public async Task EternalCounter_DirectClient_LoopsThroughMultipleIterations()
    {
        await using var host = await CreateHostAsync();
        var client = host.Services.GetRequiredService<FunctionsDurableClientProvider>().GetClient();
        const string instanceId = "eternal-direct-test";

        // Schedule the eternal counter with initial count = 0.
#pragma warning disable xUnit1051
        await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(EternalCounterFunctions.RunEternalCounter),
            input: 0,
            options: new StartOrchestrationOptions { InstanceId = instanceId });
#pragma warning restore xUnit1051

        await WaitForCustomCountAsync(client, instanceId, expectedCount: 0);

        // Raise three increment events and verify the counter advances.
        for (var i = 1; i <= 3; i++)
        {
            await client.RaiseEventAsync(instanceId, "increment", null, TestCancellation);
            await WaitForCustomCountAsync(client, instanceId, expectedCount: i);
        }

        // Terminate the eternal orchestration.
        await client.TerminateInstanceAsync(
            instanceId,
            null,
            TestCancellation);

#pragma warning disable xUnit1051
        var finalMeta = await client.WaitForInstanceCompletionAsync(instanceId);
#pragma warning restore xUnit1051
        Assert.Equal(OrchestrationRuntimeStatus.Terminated, finalMeta.RuntimeStatus);
    }

    // ── Factory & helpers ─────────────────────────────────────────────────────

    private Task<IFunctionsTestHost> CreateHostAsync()
    {
        return new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(EternalCounterFunctions).Assembly)
            .WithLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new XUnitLoggerProvider(_output))))
            .WithHostBuilderFactory(Program.CreateHostBuilder)
            .ConfigureFakeDurableSupport(typeof(EternalCounterFunctions).Assembly)
            .BuildAndStartAsync(TestCancellation);
    }

    private static async Task TerminateAndWaitAsync(IFunctionsTestHost host, string instanceId)
    {
        var client = host.Services.GetRequiredService<FunctionsDurableClientProvider>().GetClient();
        var meta = await client.GetInstancesAsync(instanceId, false, TestCancellation);
        if (meta?.RuntimeStatus is not (OrchestrationRuntimeStatus.Terminated or OrchestrationRuntimeStatus.Completed or OrchestrationRuntimeStatus.Failed))
        {
            await client.TerminateInstanceAsync(instanceId, cancellation: TestCancellation);
        }

#pragma warning disable xUnit1051
        await client.WaitForInstanceCompletionAsync(instanceId);
#pragma warning restore xUnit1051
    }

    private static async Task WaitForHttpStatusAsync(
        HttpClient httpClient,
        string statusUrl,
        string expectedStatus,
        int timeoutSeconds = 10)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken, timeout.Token);

        while (true)
        {
            using var response = await httpClient.GetAsync(statusUrl, linked.Token);
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(linked.Token);
                if (body.Contains($"\"runtimeStatus\":\"{expectedStatus}\""))
                {
                    return;
                }
            }

            await Task.Delay(50, linked.Token);
        }
    }

    private static async Task WaitForCountAsync(
        HttpClient httpClient,
        string instanceId,
        int expectedCount,
        int timeoutSeconds = 10)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken, timeout.Token);

        while (true)
        {
            using var response = await httpClient.GetAsync(
                $"/api/eternal/{instanceId}/status", linked.Token);
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(linked.Token);
                if (body.Contains($"\"count\":{expectedCount}"))
                {
                    return;
                }
            }

            await Task.Delay(50, linked.Token);
        }
    }

    private static async Task WaitForCustomCountAsync(
        DurableTaskClient client,
        string instanceId,
        int expectedCount,
        int timeoutSeconds = 10)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken, timeout.Token);

        while (true)
        {
            var meta = await client.GetInstancesAsync(instanceId, getInputsAndOutputs: true, linked.Token);
            var status = meta?.ReadCustomStatusAs<EternalCounterStatus>();
            if (status?.Count == expectedCount)
            {
                return;
            }

            await Task.Delay(25, linked.Token);
        }
    }
}
