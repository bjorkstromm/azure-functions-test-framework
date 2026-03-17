using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Durable;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace Sample.FunctionApp.Durable.Tests;

public sealed class DurableFunctionsSpikeTests
{
    [Fact]
    public async Task Invoker_GetFunctions_IncludesDurableBindings()
    {
        await using var testHost = await CreateHostAsync();

        var functions = testHost.Invoker.GetFunctions();

        var starter = Assert.Contains(nameof(DurableGreetingFunctions.StartGreetingOrchestration), functions);
        var subStarter = Assert.Contains(nameof(DurableGreetingFunctions.StartGreetingViaSubOrchestrator), functions);
        var managementStarter = Assert.Contains(nameof(DurableGreetingFunctions.StartGreetingWithManagementPayload), functions);
        var statusEndpoint = Assert.Contains(nameof(DurableGreetingFunctions.GetGreetingStatusDocument), functions);
        var orchestrator = Assert.Contains(nameof(DurableGreetingFunctions.RunGreetingOrchestration), functions);
        var parentOrchestrator = Assert.Contains(nameof(DurableGreetingFunctions.RunGreetingParentOrchestration), functions);
        var childOrchestrator = Assert.Contains(nameof(DurableGreetingFunctions.RunGreetingChildOrchestration), functions);
        var statusOrchestrator = Assert.Contains(nameof(DurableGreetingFunctions.RunGreetingStatusOrchestration), functions);
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
        Assert.Equal("activityTrigger", activity.GetDurableTriggerType());
        Assert.Equal("activityTrigger", injectedActivity.GetDurableTriggerType());
    }

    [Fact]
    public async Task HttpStarter_ReturnsOk_ForFakeDurableExecution()
    {
        await using var testHost = await CreateHostAsync();
        using var client = testHost.CreateHttpClient();

        using var response = await client.GetAsync("/api/durable/hello/martin");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Hello, martin!", content);
    }

    [Fact]
    public async Task HttpStarter_ReturnsOk_ForFakeDurableSubOrchestrationExecution()
    {
        await using var testHost = await CreateHostAsync();
        using var client = testHost.CreateHttpClient();

        using var response = await client.GetAsync("/api/durable/hello/sub/martin");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Hello, martin! (from parent)", content);
    }

    [Fact]
    public async Task DurableClientProvider_CompletesFakeOrchestration_WithExpectedOutput()
    {
        await using var testHost = await CreateHostAsync();

        var durableClientProvider = testHost.Services.GetRequiredService<FunctionsDurableClientProvider>();
        var durableClient = durableClientProvider.GetClient();

        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingOrchestration),
            "martin");

        var metadata = await durableClient.WaitForInstanceCompletionAsync(
            instanceId,
            getInputsAndOutputs: true);

        Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);
        Assert.Equal("Hello, martin!", metadata.ReadOutputAs<string>());
    }

    [Fact]
    public async Task DurableClientProvider_CompletesFakeSubOrchestration_WithExpectedOutput()
    {
        await using var testHost = await CreateHostAsync();

        var durableClientProvider = testHost.Services.GetRequiredService<FunctionsDurableClientProvider>();
        var durableClient = durableClientProvider.GetClient();

        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingParentOrchestration),
            "martin");

        var metadata = await durableClient.WaitForInstanceCompletionAsync(
            instanceId,
            getInputsAndOutputs: true);

        Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);
        Assert.Equal("Hello, martin! (from parent)", metadata.ReadOutputAs<string>());
    }

    [Fact]
    public async Task DurableClientProvider_CompletesFakeOrchestration_WithCustomStatus()
    {
        await using var testHost = await CreateHostAsync();

        var durableClientProvider = testHost.Services.GetRequiredService<FunctionsDurableClientProvider>();
        var durableClient = durableClientProvider.GetClient();

        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingStatusOrchestration),
            "martin");

        var metadata = await durableClient.WaitForInstanceCompletionAsync(
            instanceId,
            getInputsAndOutputs: true);

        var customStatus = metadata.ReadCustomStatusAs<GreetingProgressStatus>();

        Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);
        Assert.Equal("Hello, martin!", metadata.ReadOutputAs<string>());
        Assert.NotNull(customStatus);
        Assert.Equal("completed", customStatus!.Phase);
        Assert.Equal("martin", customStatus.Name);
        Assert.Equal("Hello, martin!", customStatus.Message);
    }

    [Fact]
    public async Task HttpStarter_ReturnsManagementPayload_AndStatusHelpers_ReadCustomStatus()
    {
        await using var testHost = await CreateHostAsync();
        using var client = testHost.CreateHttpClient();

        using var response = await client.GetAsync("/api/durable/manage/martin");
        var payload = await response.ReadDurableHttpManagementPayloadAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.StatusQueryGetUri));

        var status = await client.WaitForCompletionAsync(payload, TimeSpan.FromSeconds(5));
        var customStatus = status.ReadCustomStatusAs<GreetingProgressStatus>();

        Assert.Equal("Completed", status.RuntimeStatus);
        Assert.Equal("Hello, martin!", status.ReadOutputAsString());
        Assert.NotNull(customStatus);
        Assert.Equal("completed", customStatus!.Phase);
        Assert.Equal("martin", customStatus.Name);
        Assert.Equal("Hello, martin!", customStatus.Message);
    }

    [Fact]
    public async Task TestHost_InvokeActivityAsync_CompletesFakeActivity_WithExpectedOutput()
    {
        await using var testHost = await CreateHostAsync();

        var result = await testHost.InvokeActivityAsync<string>(
            nameof(DurableGreetingFunctions.CreateGreeting),
            "martin");

        Assert.Equal("Hello, martin!", result);
    }

    [Fact]
    public async Task TestHost_InvokeActivityAsync_ResolvesServices_ForInstanceActivity()
    {
        await using var testHost = await CreateHostAsync();

        var result = await testHost.InvokeActivityAsync<string>(
            nameof(InjectedGreetingActivityFunctions.CreateGreetingWithService),
            "martin");

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
}
