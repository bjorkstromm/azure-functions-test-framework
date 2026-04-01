using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Durable;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Sample.FunctionApp.Durable.Tests;

/// <summary>
/// Tests that exercise the ASP.NET Core integration path (<c>ConfigureFunctionsWebApplication</c>)
/// with durable functions. This verifies that the DI-based converter interception works when
/// <c>GrpcInvocationBridgeStartupFilter</c> fires <c>SendInvocationRequestAsync</c> without
/// synthetic durable bindings.
/// </summary>
public sealed class DurableFunctionsAspNetCoreTests
{
    private readonly ITestOutputHelper _output;

    public DurableFunctionsAspNetCoreTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task HttpStarter_ReturnsOk_ForFakeDurableExecution()
    {
        // Arrange
        await using var testHost = await CreateHostAsync();
        using var client = testHost.CreateHttpClient();

        // Act — uses the ASP.NET Core-native function (HttpRequest + IActionResult)
        using var response = await client.GetAsync("/api/aspnetcore/durable/hello/martin");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Hello, martin!", content);
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

    private Task<IFunctionsTestHost> CreateHostAsync()
    {
        return new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(DurableGreetingFunctions).Assembly)
            .WithLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new XUnitLoggerProvider(_output))))
            .WithHostBuilderFactory(Program.CreateHostBuilder)
            .ConfigureFakeDurableSupport(typeof(DurableGreetingFunctions).Assembly)
            .BuildAndStartAsync();
    }
}
