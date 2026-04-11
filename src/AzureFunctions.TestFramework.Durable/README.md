# AzureFunctions.TestFramework.Durable

[![NuGet](https://img.shields.io/nuget/v/AzureFunctions.TestFramework.Durable.svg)](https://www.nuget.org/packages/AzureFunctions.TestFramework.Durable)

Fake-backed Durable Functions test helpers for the Azure Functions Test Framework. Includes `ConfigureFakeDurableSupport(...)`, `FakeDurableTaskClient`, direct activity invocation, fake orchestration scheduling, sub-orchestrators, external events, and HTTP status polling helpers.

No real Durable Task runtime or external storage (Azure Storage / Netherite / MSSQL) is needed.

## Why fake-backed?

The real Durable Task execution engine relies on external storage and the Durable Task Framework host, neither of which runs inside the test framework's in-process worker. `ConfigureFakeDurableSupport(...)` registers a `FakeDurableTaskClient` and companion types that intercept `[DurableClient]` binding resolution at the DI level, letting starter functions, orchestrators, activities, and sub-orchestrators execute fully in-process.

## Setup

```csharp
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Durable;

_testHost = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyDurableFunction).Assembly)
    .ConfigureFakeDurableSupport(provider =>
    {
        // Register orchestration runners
        provider.AddOrchestration<string>("MyOrchestrator", ctx => MyOrchestratorFunction.RunAsync(ctx));
    })
    .BuildAndStartAsync();
```

## Coverage

- `[DurableClient] DurableTaskClient` injection (both direct gRPC and ASP.NET Core integration paths)
- Direct activity invocation via `IFunctionsTestHost.InvokeActivityAsync<TResult>(...)`
- Fake orchestration scheduling and activity execution
- Sub-orchestrator execution via `TaskOrchestrationContext.CallSubOrchestratorAsync<TResult>()`
- Custom status via `SetCustomStatus(...)` and `OrchestrationMetadata.ReadCustomStatusAs<T>()`
- External events: both wait-then-raise and buffered raise-before-wait flows
- `FunctionsDurableClientProvider` resolution from `FunctionsTestHost.Services`

## Example

```csharp
public class DurableFunctionTests : IAsyncLifetime
{
    private IFunctionsTestHost _testHost;

    public async Task InitializeAsync()
    {
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(OrderOrchestrator).Assembly)
            .WithHostBuilderFactory(Program.CreateHostBuilder)
            .ConfigureFakeDurableSupport(provider =>
            {
                provider.AddOrchestration<OrderResult>("OrderOrchestrator",
                    ctx => OrderOrchestratorFunction.RunAsync(ctx));
                provider.AddActivity<string, OrderResult>("ProcessOrder",
                    input => new OrderResult { Success = true, OrderId = input });
            })
            .BuildAndStartAsync();
    }

    [Fact]
    public async Task StartOrder_StartsOrchestration_AndCompletes()
    {
        // Start via HTTP starter function
        using var client = _testHost.CreateHttpClient();
        var response = await client.PostAsync("/api/orders/start", JsonContent.Create(new { orderId = "123" }));
        response.EnsureSuccessStatusCode();

        var instanceId = await response.Content.ReadAsStringAsync();

        // Resolve the durable client and check status
        var durableClient = _testHost.Services
            .GetRequiredService<FunctionsDurableClientProvider>()
            .GetClient();

        var metadata = await durableClient.GetInstanceAsync(instanceId);
        Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata!.RuntimeStatus);
    }

    [Fact]
    public async Task ProcessOrder_Activity_ReturnsResult()
    {
        // Invoke activity directly
        var result = await _testHost.InvokeActivityAsync<OrderResult>("ProcessOrder", "order-123");
        Assert.True(result.Success);
        Assert.Equal("order-123", result.OrderId);
    }

    [Fact]
    public async Task OrderOrchestrator_WithExternalEvent_Completes()
    {
        var durableClient = _testHost.Services
            .GetRequiredService<FunctionsDurableClientProvider>()
            .GetClient() as FakeDurableTaskClient;

        var instanceId = await durableClient!.ScheduleNewOrchestrationInstanceAsync("OrderOrchestrator", "order-123");

        // Raise external event to unblock orchestrator
        await durableClient.RaiseEventAsync(instanceId, "ApprovalReceived", true);

        var metadata = await durableClient.GetInstanceAsync(instanceId);
        Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata!.RuntimeStatus);
    }

    public async Task DisposeAsync()
    {
        await _testHost.StopAsync();
        _testHost.Dispose();
    }
}
```

## References

- [Full documentation](https://github.com/bjorkstromm/azure-functions-test-framework)
- [`AzureFunctions.TestFramework.Core`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Core)

## License

MIT
