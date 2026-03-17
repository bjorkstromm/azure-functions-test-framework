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
        var orchestrator = Assert.Contains(nameof(DurableGreetingFunctions.RunGreetingOrchestration), functions);
        var activity = Assert.Contains(nameof(DurableGreetingFunctions.CreateGreeting), functions);

        Assert.True(starter.HasBindingType("httpTrigger"));
        Assert.True(starter.HasBindingType("durableClient"));
        Assert.Equal("orchestrationTrigger", orchestrator.GetDurableTriggerType());
        Assert.Equal("activityTrigger", activity.GetDurableTriggerType());
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

    private Task<IFunctionsTestHost> CreateHostAsync()
    {
        return new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(DurableGreetingFunctions).Assembly)
            .WithHostBuilderFactory(Program.CreateWorkerHostBuilder)
            .ConfigureFakeDurableSupport(typeof(DurableGreetingFunctions).Assembly)
            .BuildAndStartAsync();
    }
}
