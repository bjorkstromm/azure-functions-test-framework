using Azure.Core.Serialization;
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Durable;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sample.FunctionApp.Durable;
using System.Net;
using System.Text.Json;

namespace Sample.FunctionApp.Durable.Tests;

/// <summary>
/// Verifies that configuring a custom <see cref="JsonObjectSerializer"/> on the worker does not
/// break fake-durable orchestration flows, and that the fake durable runner correctly serializes
/// and deserializes complex typed inputs and outputs through activity boundaries.
/// </summary>
public sealed class DurableCustomSerializerTests
{
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly ITestOutputHelper _output;

    public DurableCustomSerializerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private Task<IFunctionsTestHost> CreateHostAsync(bool useCustomSerializer = false)
    {
        var builder = new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(DurableGreetingFunctions).Assembly)
            .WithLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new XUnitLoggerProvider(_output))))
            // Use ConfigureFunctionsWorkerDefaults (direct gRPC mode) so string return values from
            // HTTP trigger functions are correctly forwarded via the gRPC invocation response path.
            .WithHostBuilderFactory(args => new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureServices(s => s.AddSingleton<GreetingFormatter>()))
            .ConfigureFakeDurableSupport(typeof(DurableGreetingFunctions).Assembly);

        if (useCustomSerializer)
        {
            builder.ConfigureServices(services =>
                services.Configure<WorkerOptions>(opts =>
                    opts.Serializer = new JsonObjectSerializer(SnakeCaseOptions)));
        }

        return builder.BuildAndStartAsync(TestCancellation);
    }

    /// <summary>
    /// When a custom snake_case serializer is configured, a simple string-output orchestration
    /// must still complete successfully.  This verifies the fake durable runner is not broken by
    /// the custom serializer configuration.
    /// </summary>
    [Fact]
    public async Task DurableWithCustomSerializer_StringOrchestration_CompletesSuccessfully()
    {
        await using var testHost = await CreateHostAsync(useCustomSerializer: true);

        var durableClientProvider = testHost.Services.GetRequiredService<FunctionsDurableClientProvider>();
        var durableClient = durableClientProvider.GetClient();

#pragma warning disable xUnit1051
        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingOrchestration),
            "world");

        var metadata = await durableClient.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true);
#pragma warning restore xUnit1051

        Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);
        Assert.Equal("Hello, world!", metadata.ReadOutputAs<string>());
    }

    /// <summary>
    /// When a custom snake_case serializer is configured, the HTTP durable starter must still
    /// invoke the function successfully and return the expected greeting.
    /// </summary>
    [Fact]
    public async Task DurableWithCustomSerializer_HttpStarter_ReturnsCorrectGreeting()
    {
        await using var testHost = await CreateHostAsync(useCustomSerializer: true);
        using var client = testHost.CreateHttpClient();

        using var response = await client.GetAsync("/api/durable/hello/world", TestCancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(TestCancellation);
        Assert.Equal("Hello, world!", content);
    }

    /// <summary>
    /// Verifies that the fake durable runner correctly serializes and deserializes complex typed
    /// inputs and outputs through activity/orchestration boundaries.  This ensures that the
    /// <c>ConvertValue</c> mechanism in <c>FakeDurableOrchestrationRunner</c> handles complex
    /// record types and preserves all property values.
    /// </summary>
    [Fact]
    public async Task DurableWithComplexTypedIO_OrchestrationPreservesValues()
    {
        await using var testHost = await CreateHostAsync();

        var durableClientProvider = testHost.Services.GetRequiredService<FunctionsDurableClientProvider>();
        var durableClient = durableClientProvider.GetClient();

        var input = new TypedGreetingInput("Alice", RepeatCount: 3);

#pragma warning disable xUnit1051
        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableTypedFunctions.RunTypedGreetingOrchestration),
            input);

        var metadata = await durableClient.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true);
#pragma warning restore xUnit1051

        Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);

        var result = metadata.ReadOutputAs<TypedGreetingOutput>();
        Assert.NotNull(result);
        Assert.Equal("Hello, Alice!", result.Greeting);
        Assert.Equal("Alice", result.Recipient);
        Assert.Equal(3, result.RepeatCount);
    }

    /// <summary>
    /// Verifies that the fake durable runner correctly handles complex typed I/O even when a
    /// custom snake_case serializer is configured on the worker.
    /// </summary>
    [Fact]
    public async Task DurableWithComplexTypedIO_AndCustomSerializer_OrchestrationPreservesValues()
    {
        await using var testHost = await CreateHostAsync(useCustomSerializer: true);

        var durableClientProvider = testHost.Services.GetRequiredService<FunctionsDurableClientProvider>();
        var durableClient = durableClientProvider.GetClient();

        var input = new TypedGreetingInput("Bob", RepeatCount: 2);

#pragma warning disable xUnit1051
        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableTypedFunctions.RunTypedGreetingOrchestration),
            input);

        var metadata = await durableClient.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true);
#pragma warning restore xUnit1051

        Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);

        var result = metadata.ReadOutputAs<TypedGreetingOutput>();
        Assert.NotNull(result);
        Assert.Equal("Hello, Bob!", result.Greeting);
        Assert.Equal("Bob", result.Recipient);
        Assert.Equal(2, result.RepeatCount);
    }
}
