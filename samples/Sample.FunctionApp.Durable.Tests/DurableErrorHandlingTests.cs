using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Durable;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Sample.FunctionApp.Durable.Tests;

/// <summary>
/// Integration tests that exercise error-handling paths and edge-case APIs in the fake durable
/// runner: failing orchestrations, failing activities, <c>CreateTimer</c> as a no-op,
/// <c>SendEvent</c> not-supported, unsupported client methods, non-existent-instance guards,
/// and missing-durable-support guard conditions.
/// </summary>
public sealed class DurableErrorHandlingTests
{
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public DurableErrorHandlingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ── Failing orchestrations ────────────────────────────────────────────────

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task FailingOrchestration_StatusIsFailed_WithFailureDetails()
    {
        // Arrange
        await using var host = await CreateHostAsync();
        var client = GetDurableClient(host);

#pragma warning disable xUnit1051
        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableErrorFunctions.ThrowingOrchestration));
        var metadata = await client.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true);
#pragma warning restore xUnit1051

        // Assert
        Assert.Equal(OrchestrationRuntimeStatus.Failed, metadata.RuntimeStatus);
        Assert.NotNull(metadata.FailureDetails);
        Assert.False(string.IsNullOrEmpty(metadata.FailureDetails!.ErrorMessage));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task FailingActivity_PropagatesAsOrchestrationFailure_WithFailureDetails()
    {
        // Arrange
        await using var host = await CreateHostAsync();
        var client = GetDurableClient(host);

#pragma warning disable xUnit1051
        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableErrorFunctions.ActivityThrowingOrchestration));
        var metadata = await client.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true);
#pragma warning restore xUnit1051

        // Assert — the orchestration itself fails because the activity threw
        Assert.Equal(OrchestrationRuntimeStatus.Failed, metadata.RuntimeStatus);
        Assert.NotNull(metadata.FailureDetails);
        Assert.False(string.IsNullOrEmpty(metadata.FailureDetails!.ErrorMessage));
    }

    // ── CreateTimer (no-op) ───────────────────────────────────────────────────

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task Orchestration_WithCreateTimer_CompletesSuccessfully()
    {
        // Arrange — CreateTimer is a no-op in the fake runner; the orchestration should
        // complete immediately rather than blocking for 60 seconds.
        await using var host = await CreateHostAsync();
        var client = GetDurableClient(host);

#pragma warning disable xUnit1051
        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableErrorFunctions.TimerOrchestration));
        var metadata = await client.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true);
#pragma warning restore xUnit1051

        Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);
        Assert.Equal("timer-completed", metadata.ReadOutputAs<string>());
    }

    // ── SendEvent (supported) ─────────────────────────────────────────────────

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task Orchestration_WithSendEvent_CompletesSuccessfully()
    {
        // Arrange — SendEvent is supported by the fake runner.
        await using var host = await CreateHostAsync();
        var client = GetDurableClient(host);

#pragma warning disable xUnit1051
        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableErrorFunctions.SendEventOrchestration));
        var metadata = await client.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true);
#pragma warning restore xUnit1051

        Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);
        Assert.Null(metadata.FailureDetails);
    }

    // ── Client query APIs ─────────────────────────────────────────────────────

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task GetAllInstancesAsync_ReturnsResults()
    {
        await using var host = await CreateHostAsync();
        var client = GetDurableClient(host);

#pragma warning disable xUnit1051
        await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingOrchestration),
            "query-test");
#pragma warning restore xUnit1051

        var instances = await ToListAsync(client.GetAllInstancesAsync(), TestCancellation);
        Assert.NotEmpty(instances);
    }

    // ── Non-existent instance guard ───────────────────────────────────────────

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task GetInstancesAsync_NonExistentInstance_ReturnsNull()
    {
        await using var host = await CreateHostAsync();
        var client = GetDurableClient(host);

        var metadata = await client.GetInstancesAsync("does-not-exist", false, TestCancellation);

        Assert.Null(metadata);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task RaiseEventAsync_NonExistentInstance_ThrowsInvalidOperationException()
    {
        await using var host = await CreateHostAsync();
        var client = GetDurableClient(host);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.RaiseEventAsync("does-not-exist", "some-event", null, TestCancellation));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task SuspendInstanceAsync_NonExistentInstance_ThrowsInvalidOperationException()
    {
        await using var host = await CreateHostAsync();
        var client = GetDurableClient(host);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SuspendInstanceAsync("does-not-exist", reason: null, TestCancellation));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task ResumeInstanceAsync_NonExistentInstance_ThrowsInvalidOperationException()
    {
        await using var host = await CreateHostAsync();
        var client = GetDurableClient(host);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ResumeInstanceAsync("does-not-exist", reason: null, TestCancellation));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task TerminateInstanceAsync_NonExistentInstance_ThrowsInvalidOperationException()
    {
        await using var host = await CreateHostAsync();
        var client = GetDurableClient(host);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.TerminateInstanceAsync("does-not-exist", cancellation: TestCancellation));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task WaitForInstanceCompletionAsync_NonExistentInstance_ThrowsInvalidOperationException()
    {
        await using var host = await CreateHostAsync();
        var client = GetDurableClient(host);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
#pragma warning disable xUnit1051
            client.WaitForInstanceCompletionAsync("does-not-exist"));
#pragma warning restore xUnit1051
    }

    // ── Unknown function name guard ───────────────────────────────────────────

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task InvokeActivityAsync_UnknownFunctionName_ThrowsInvalidOperationException()
    {
        await using var host = await CreateHostAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.InvokeActivityAsync<string>("NonExistentActivity", "input", TestCancellation));
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

    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source, CancellationToken cancellationToken)
    {
        List<T> items = [];
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            items.Add(item);
        }

        return items;
    }
}
