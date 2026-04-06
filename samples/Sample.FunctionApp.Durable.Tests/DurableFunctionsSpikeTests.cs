using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Durable;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Sample.FunctionApp.Durable.Tests;

public sealed class DurableFunctionsSpikeTests
{
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    private readonly ITestOutputHelper _output;

    public DurableFunctionsSpikeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task HttpStarter_ReturnsOk_ForFakeDurableExecution()
    {
        // Arrange
        await using var testHost = await CreateHostAsync();
        using var client = testHost.CreateHttpClient();

        // Act
        using var response = await client.GetAsync("/api/durable/hello/martin", TestCancellation);
        var content = await response.Content.ReadAsStringAsync(TestCancellation);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Hello, martin!", content);
    }

    [Fact]
    public async Task HttpStarter_ReturnsOk_ForFakeDurableSubOrchestrationExecution()
    {
        // Arrange
        await using var testHost = await CreateHostAsync();
        using var client = testHost.CreateHttpClient();

        // Act
        using var response = await client.GetAsync("/api/durable/hello/sub/martin", TestCancellation);
        var content = await response.Content.ReadAsStringAsync(TestCancellation);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Hello, martin! (from parent)", content);
    }

    [Fact]
    public async Task DurableClientProvider_CompletesFakeOrchestration_WithExpectedOutput()
    {
        // Arrange
        await using var testHost = await CreateHostAsync();

        var durableClientProvider = testHost.Services.GetRequiredService<FunctionsDurableClientProvider>();
        var durableClient = durableClientProvider.GetClient();

        // Act
#pragma warning disable xUnit1051 // ScheduleNewOrchestrationInstanceAsync has no CancellationToken overload on DurableTaskClient.
        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingOrchestration),
            "martin");
#pragma warning restore xUnit1051

#pragma warning disable xUnit1051 // DurableTaskClient.WaitForInstanceCompletionAsync has no CancellationToken overload in this package version.
        var metadata = await durableClient.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true);
#pragma warning restore xUnit1051

        // Assert
        Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);
        Assert.Equal("Hello, martin!", metadata.ReadOutputAs<string>());
    }

    [Fact]
    public async Task DurableClientProvider_CompletesFakeSubOrchestration_WithExpectedOutput()
    {
        // Arrange
        await using var testHost = await CreateHostAsync();

        var durableClientProvider = testHost.Services.GetRequiredService<FunctionsDurableClientProvider>();
        var durableClient = durableClientProvider.GetClient();

        // Act
#pragma warning disable xUnit1051 // ScheduleNewOrchestrationInstanceAsync has no CancellationToken overload on DurableTaskClient.
        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingParentOrchestration),
            "martin");
#pragma warning restore xUnit1051

#pragma warning disable xUnit1051 // DurableTaskClient.WaitForInstanceCompletionAsync has no CancellationToken overload in this package version.
        var metadata = await durableClient.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true);
#pragma warning restore xUnit1051

        // Assert
        Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);
        Assert.Equal("Hello, martin! (from parent)", metadata.ReadOutputAs<string>());
    }

    [Fact]
    public async Task DurableClientProvider_CompletesFakeOrchestration_WithCustomStatus()
    {
        // Arrange
        await using var testHost = await CreateHostAsync();

        var durableClientProvider = testHost.Services.GetRequiredService<FunctionsDurableClientProvider>();
        var durableClient = durableClientProvider.GetClient();

        // Act
#pragma warning disable xUnit1051 // ScheduleNewOrchestrationInstanceAsync has no CancellationToken overload on DurableTaskClient.
        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingStatusOrchestration),
            "martin");
#pragma warning restore xUnit1051

#pragma warning disable xUnit1051 // DurableTaskClient.WaitForInstanceCompletionAsync has no CancellationToken overload in this package version.
        var metadata = await durableClient.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true);
#pragma warning restore xUnit1051

        var customStatus = metadata.ReadCustomStatusAs<GreetingProgressStatus>();

        // Assert
        Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);
        Assert.Equal("Hello, martin!", metadata.ReadOutputAs<string>());
        Assert.NotNull(customStatus);
        await Verify(customStatus);
    }

    [Fact]
    public async Task HttpStarter_ReturnsManagementPayload_AndStatusHelpers_ReadCustomStatus()
    {
        // Arrange
        await using var testHost = await CreateHostAsync();
        using var client = testHost.CreateHttpClient();

        // Act
        using var response = await client.GetAsync("/api/durable/manage/martin", TestCancellation);
        var payload = await response.ReadDurableHttpManagementPayloadAsync(TestCancellation);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.StatusQueryGetUri));

        var status = await client.WaitForCompletionAsync(
            payload,
            TimeSpan.FromSeconds(5),
            cancellationToken: TestCancellation);
        var customStatus = status.ReadCustomStatusAs<GreetingProgressStatus>();

        Assert.Equal("Completed", status.RuntimeStatus);
        Assert.Equal("Hello, martin!", status.ReadOutputAsString());
        Assert.NotNull(customStatus);
        await Verify(customStatus);
    }

    [Fact]
    public async Task DurableClientProvider_CompletesFakeOrchestration_AfterExternalEvent()
    {
        // Arrange
        await using var testHost = await CreateHostAsync();

        var durableClientProvider = testHost.Services.GetRequiredService<FunctionsDurableClientProvider>();
        var durableClient = durableClientProvider.GetClient();

#pragma warning disable xUnit1051 // ScheduleNewOrchestrationInstanceAsync has no CancellationToken overload on DurableTaskClient.
        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingAwaitEventOrchestration),
            "martin");
#pragma warning restore xUnit1051

#pragma warning disable xUnit1051 // DurableTaskClient.WaitForInstanceStartAsync has no CancellationToken overload in this package version.
        await durableClient.WaitForInstanceStartAsync(instanceId, getInputsAndOutputs: true);
#pragma warning restore xUnit1051

        var waitingStatus = await WaitForCustomStatusAsync(
            durableClient,
            instanceId,
            status => status?.Phase == "waiting-for-event");

        Assert.NotNull(waitingStatus);
        await Verify(waitingStatus).UseMethodName(nameof(DurableClientProvider_CompletesFakeOrchestration_AfterExternalEvent) + "_waiting");

        // Act
#pragma warning disable xUnit1051 // DurableTaskClient.RaiseEventAsync has no CancellationToken overload in this package version.
        await durableClient.RaiseEventAsync(
            instanceId,
            "greeting-suffix",
            new GreetingSuffixEvent("from event"));
#pragma warning restore xUnit1051

#pragma warning disable xUnit1051 // DurableTaskClient.WaitForInstanceCompletionAsync has no CancellationToken overload in this package version.
        var metadata = await durableClient.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true);
#pragma warning restore xUnit1051

        var completedStatus = metadata.ReadCustomStatusAs<GreetingProgressStatus>();

        // Assert
        Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);
        Assert.Equal("Hello, martin! (from event)", metadata.ReadOutputAs<string>());
        Assert.NotNull(completedStatus);
        await Verify(completedStatus).UseMethodName(nameof(DurableClientProvider_CompletesFakeOrchestration_AfterExternalEvent) + "_completed");
    }

    [Fact]
    public async Task DurableClientProvider_CompletesFakeOrchestration_AfterBufferedExternalEvent()
    {
        // Arrange
        await using var testHost = await CreateHostAsync();

        var durableClientProvider = testHost.Services.GetRequiredService<FunctionsDurableClientProvider>();
        var durableClient = durableClientProvider.GetClient();

#pragma warning disable xUnit1051 // ScheduleNewOrchestrationInstanceAsync has no CancellationToken overload on DurableTaskClient.
        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingAwaitEventOrchestration),
            "martin");
#pragma warning restore xUnit1051

#pragma warning disable xUnit1051 // DurableTaskClient.WaitForInstanceStartAsync has no CancellationToken overload in this package version.
        await durableClient.WaitForInstanceStartAsync(instanceId, getInputsAndOutputs: true);
#pragma warning restore xUnit1051

        // Act
#pragma warning disable xUnit1051 // DurableTaskClient.RaiseEventAsync has no CancellationToken overload in this package version.
        await durableClient.RaiseEventAsync(
            instanceId,
            "greeting-suffix",
            new GreetingSuffixEvent("from buffered event"));
#pragma warning restore xUnit1051

#pragma warning disable xUnit1051 // DurableTaskClient.WaitForInstanceCompletionAsync has no CancellationToken overload in this package version.
        var metadata = await durableClient.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true);
#pragma warning restore xUnit1051

        var completedStatus = metadata.ReadCustomStatusAs<GreetingProgressStatus>();

        // Assert
        Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);
        Assert.Equal("Hello, martin! (from buffered event)", metadata.ReadOutputAs<string>());
        Assert.NotNull(completedStatus);
        await Verify(completedStatus);
    }

    [Fact]
    public async Task TestHost_InvokeActivityAsync_CompletesFakeActivity_WithExpectedOutput()
    {
        // Arrange
        await using var testHost = await CreateHostAsync();

        // Act
        var result = await testHost.InvokeActivityAsync<string>(
            nameof(DurableGreetingFunctions.CreateGreeting),
            "martin",
            TestCancellation);

        // Assert
        Assert.Equal("Hello, martin!", result);
    }

    [Fact]
    public async Task TestHost_InvokeActivityAsync_ResolvesServices_ForInstanceActivity()
    {
        // Arrange
        await using var testHost = await CreateHostAsync();

        // Act
        var result = await testHost.InvokeActivityAsync<string>(
            nameof(InjectedGreetingActivityFunctions.CreateGreetingWithService),
            "martin",
            TestCancellation);

        // Assert
        Assert.Equal("Hello, martin! (from service)", result);
    }

    private Task<IFunctionsTestHost> CreateHostAsync()
    {
        return new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(DurableGreetingFunctions).Assembly)
            .WithLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new XUnitLoggerProvider(_output))))
            .WithHostBuilderFactory(Program.CreateWorkerHostBuilder)
            .ConfigureFakeDurableSupport(typeof(DurableGreetingFunctions).Assembly)
            .BuildAndStartAsync(TestCancellation);
    }

    private static async Task<GreetingProgressStatus?> WaitForCustomStatusAsync(
        DurableTaskClient durableClient,
        string instanceId,
        Func<GreetingProgressStatus?, bool> predicate)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken,
            timeoutCts.Token);

        while (true)
        {
            var metadata = await durableClient.GetInstancesAsync(
                instanceId,
                getInputsAndOutputs: true,
                linked.Token);

            var status = metadata?.ReadCustomStatusAs<GreetingProgressStatus>();
            if (predicate(status))
            {
                return status;
            }

            await Task.Delay(50, linked.Token);
        }
    }
}
