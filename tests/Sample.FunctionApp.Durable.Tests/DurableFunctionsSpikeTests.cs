using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Durable;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using VerifyXunit;
using Xunit;

namespace Sample.FunctionApp.Durable.Tests;

public sealed class DurableFunctionsSpikeTests
{
    [Fact]
    public async Task Invoker_GetFunctions_IncludesDurableBindings()
    {
        // Arrange
        await using var testHost = await CreateHostAsync();

        // Act
        var functions = testHost.Invoker.GetFunctions();

        // Assert
        var starter = Assert.Contains(nameof(DurableGreetingFunctions.StartGreetingOrchestration), functions);
        var subStarter = Assert.Contains(nameof(DurableGreetingFunctions.StartGreetingViaSubOrchestrator), functions);
        var managementStarter = Assert.Contains(nameof(DurableGreetingFunctions.StartGreetingWithManagementPayload), functions);
        var statusEndpoint = Assert.Contains(nameof(DurableGreetingFunctions.GetGreetingStatusDocument), functions);
        var orchestrator = Assert.Contains(nameof(DurableGreetingFunctions.RunGreetingOrchestration), functions);
        var parentOrchestrator = Assert.Contains(nameof(DurableGreetingFunctions.RunGreetingParentOrchestration), functions);
        var childOrchestrator = Assert.Contains(nameof(DurableGreetingFunctions.RunGreetingChildOrchestration), functions);
        var statusOrchestrator = Assert.Contains(nameof(DurableGreetingFunctions.RunGreetingStatusOrchestration), functions);
        var eventOrchestrator = Assert.Contains(nameof(DurableGreetingFunctions.RunGreetingAwaitEventOrchestration), functions);
        var activity = Assert.Contains(nameof(DurableGreetingFunctions.CreateGreeting), functions);
        var injectedActivity = Assert.Contains(nameof(InjectedGreetingActivityFunctions.CreateGreetingWithService), functions);

        Assert.True(starter.HasBindingType("httpTrigger"));
        Assert.True(starter.HasBindingType("durableClient"));
        Assert.True(subStarter.HasBindingType("httpTrigger"));
        Assert.True(subStarter.HasBindingType("durableClient"));
        Assert.True(managementStarter.HasBindingType("httpTrigger"));
        Assert.True(managementStarter.HasBindingType("durableClient"));
        Assert.True(statusEndpoint.HasBindingType("httpTrigger"));
        Assert.True(statusEndpoint.HasBindingType("durableClient"));
        Assert.Equal("orchestrationTrigger", orchestrator.GetDurableTriggerType());
        Assert.Equal("orchestrationTrigger", parentOrchestrator.GetDurableTriggerType());
        Assert.Equal("orchestrationTrigger", childOrchestrator.GetDurableTriggerType());
        Assert.Equal("orchestrationTrigger", statusOrchestrator.GetDurableTriggerType());
        Assert.Equal("orchestrationTrigger", eventOrchestrator.GetDurableTriggerType());
        Assert.Equal("activityTrigger", activity.GetDurableTriggerType());
        Assert.Equal("activityTrigger", injectedActivity.GetDurableTriggerType());
    }

    [Fact]
    public async Task HttpStarter_ReturnsOk_ForFakeDurableExecution()
    {
        // Arrange
        await using var testHost = await CreateHostAsync();
        using var client = testHost.CreateHttpClient();

        // Act
        using var response = await client.GetAsync("/api/durable/hello/martin");
        var content = await response.Content.ReadAsStringAsync();

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
        using var response = await client.GetAsync("/api/durable/hello/sub/martin");
        var content = await response.Content.ReadAsStringAsync();

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
        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingOrchestration),
            "martin");

        var metadata = await durableClient.WaitForInstanceCompletionAsync(
            instanceId,
            getInputsAndOutputs: true);

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
        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingParentOrchestration),
            "martin");

        var metadata = await durableClient.WaitForInstanceCompletionAsync(
            instanceId,
            getInputsAndOutputs: true);

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
        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingStatusOrchestration),
            "martin");

        var metadata = await durableClient.WaitForInstanceCompletionAsync(
            instanceId,
            getInputsAndOutputs: true);

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
        using var response = await client.GetAsync("/api/durable/manage/martin");
        var payload = await response.ReadDurableHttpManagementPayloadAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.StatusQueryGetUri));

        var status = await client.WaitForCompletionAsync(payload, TimeSpan.FromSeconds(5));
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

        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingAwaitEventOrchestration),
            "martin");

        await durableClient.WaitForInstanceStartAsync(instanceId, getInputsAndOutputs: true);

        var waitingStatus = await WaitForCustomStatusAsync(
            durableClient,
            instanceId,
            status => status?.Phase == "waiting-for-event");

        Assert.NotNull(waitingStatus);
        await Verify(waitingStatus).UseMethodName(nameof(DurableClientProvider_CompletesFakeOrchestration_AfterExternalEvent) + "_waiting");

        // Act
        await durableClient.RaiseEventAsync(
            instanceId,
            "greeting-suffix",
            new GreetingSuffixEvent("from event"));

        var metadata = await durableClient.WaitForInstanceCompletionAsync(
            instanceId,
            getInputsAndOutputs: true);

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

        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingAwaitEventOrchestration),
            "martin");

        await durableClient.WaitForInstanceStartAsync(instanceId, getInputsAndOutputs: true);

        // Act
        await durableClient.RaiseEventAsync(
            instanceId,
            "greeting-suffix",
            new GreetingSuffixEvent("from buffered event"));

        var metadata = await durableClient.WaitForInstanceCompletionAsync(
            instanceId,
            getInputsAndOutputs: true);

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
            "martin");

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
            "martin");

        // Assert
        Assert.Equal("Hello, martin! (from service)", result);
    }

    private Task<IFunctionsTestHost> CreateHostAsync()
    {
        return new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(DurableGreetingFunctions).Assembly)
            .WithHostBuilderFactory(Program.CreateWorkerHostBuilder)
            .ConfigureFakeDurableSupport(typeof(DurableGreetingFunctions).Assembly)
            .BuildAndStartAsync();
    }

    private static async Task<GreetingProgressStatus?> WaitForCustomStatusAsync(
        DurableTaskClient durableClient,
        string instanceId,
        Func<GreetingProgressStatus?, bool> predicate)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (true)
        {
            var metadata = await durableClient.GetInstancesAsync(
                instanceId,
                getInputsAndOutputs: true,
                timeoutCts.Token);

            var status = metadata?.ReadCustomStatusAs<GreetingProgressStatus>();
            if (predicate(status))
            {
                return status;
            }

            await Task.Delay(50, timeoutCts.Token);
        }
    }
}
