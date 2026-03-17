# Azure Functions Test Framework

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

An integration testing framework for Azure Functions (dotnet-isolated) that provides a TestServer/WebApplicationFactory-like experience.

## ⚠️ Project Status: Early Development

**Current Status**: Both testing approaches are fully functional for **Worker SDK 2.x (.NET 9)**. The gRPC-based `FunctionsTestHost` supports full CRUD HTTP invocations, timer/queue/service-bus/blob/event-grid trigger invocations, function metadata discovery, service-provider access via `Services`, configuration overrides via `ConfigureSetting` and `ConfigureEnvironmentVariable`, and `WithHostBuilderFactory` supporting both `ConfigureFunctionsWorkerDefaults()` and `ConfigureFunctionsWebApplication()` modes. `FunctionsWebApplicationFactory` supports full CRUD including POST/PUT/DELETE and `WithWebHostBuilder` service overrides. The sample app includes `CorrelationIdMiddleware`, configuration endpoints, and an opt-in shared-host gRPC fixture example. Startup/readiness is event-driven instead of delay/poll based, the direct gRPC request path precompiles route templates and reuses handlers per host, and WAF host shutdown uses a short timeout plus an explicit async disposal path. All framework libraries target `net8.0;net9.0;net10.0` and are published as NuGet packages to NuGet.org (versioned via MinVer from git tags). A separate Durable Functions spike now exists via `AzureFunctions.TestFramework.Durable`, `Sample.FunctionApp.Durable`, and `Sample.FunctionApp.Durable.Tests`; it provides a fake-backed, fully in-process starter/orchestrator/activity path centered on `ConfigureFakeDurableSupport(...)`, supports Azure-style `[DurableClient] DurableTaskClient` injection, sub-orchestrators, direct activity invocation via `IFunctionsTestHost` extensions, custom-status/status-helper coverage, and wait-then-raise external events. See [KNOWN_ISSUES.md](KNOWN_ISSUES.md) for details.

### What Works ✅
- gRPC server starts and accepts connections
- Azure Functions Worker runs in-process using HostBuilder
- Worker connects successfully to gRPC server and loads all functions
- Bidirectional gRPC streaming functional
- HTTP client API (`FunctionsTestHostBuilder` + `CreateHttpClient()`) functional for all HTTP methods (GET, POST, PUT, DELETE)
- `FunctionsWebApplicationFactory<TProgram>` functional for all HTTP methods via ASP.NET Core `TestServer`
- Route matching with `{param}` placeholder support in both approaches
- `WithWebHostBuilder` DI service overrides work end-to-end
- CI workflow runs on pull requests and pushes to main
- Tests run in parallel (xUnit `parallelizeTestCollections`) and in isolation (per-test host via `IAsyncLifetime` for gRPC tests; shared async-aware WAF fixture + per-test `InMemoryTodoService.Reset()` for WAF tests)
- gRPC EventStream shuts down gracefully on test teardown (no connection-abort errors, no Kestrel 5 s shutdown wait)
- **TimerTrigger invocations** via `AzureFunctions.TestFramework.Timer` package (`InvokeTimerAsync` extension method)
- **ServiceBus trigger invocations** via `AzureFunctions.TestFramework.ServiceBus` package (`InvokeServiceBusAsync` extension method)
- **Queue trigger invocations** via `AzureFunctions.TestFramework.Queue` package (`InvokeQueueAsync` extension method)
- **Blob trigger invocations** via `AzureFunctions.TestFramework.Blob` package (`InvokeBlobAsync` extension method)
- **Event Grid trigger invocations** via `AzureFunctions.TestFramework.EventGrid` package (`InvokeEventGridAsync` extension method, supports both `EventGridEvent` and `CloudEvent` schemas)
- **Function metadata discovery** via `IFunctionInvoker.GetFunctions()` — returns `IReadOnlyDictionary<string, IFunctionMetadata>` with name, function ID, entry point, script file, and raw binding JSON
- **`WithHostBuilderFactory`** — non-WAF gRPC tests can reuse the app's own `Program.CreateWorkerHostBuilder` or `Program.CreateHostBuilder`, automatically inheriting all registered services without re-registering them in each test. Both `ConfigureFunctionsWorkerDefaults()` and `ConfigureFunctionsWebApplication()` are supported.
- **`FunctionsTestHost.Services`** — access the worker's configured service provider after startup to resolve singletons and inspect test state directly
- **Configuration overrides** — `FunctionsTestHostBuilder.ConfigureSetting(key, value)` overlays test-specific configuration values into the worker host
- **Environment variable overrides** — `FunctionsTestHostBuilder.ConfigureEnvironmentVariable(name, value)` sets process-level environment variables before the worker starts, so functions can read them via `IConfiguration` or `Environment.GetEnvironmentVariable`
- **Middleware sample + testing** — `Sample.FunctionApp.Worker` registers a `CorrelationIdMiddleware` that copies `x-correlation-id` into `FunctionContext.Items`, and both Worker test projects assert it through `/api/correlation`
- **Performance improvements** — startup waits now use worker connection/function-load signals instead of fixed delays, direct gRPC route matching is precompiled per host, `CreateHttpClient()` reuses host-local handlers, and WAF test shutdown no longer falls back to the slow base `WithWebHostBuilder` clone path
- **Durable Functions spike** — `AzureFunctions.TestFramework.Durable` provides fake-backed durable test services (`ConfigureFakeDurableSupport(...)`, native `[DurableClient]` binding support, `FunctionsDurableClientProvider`, `InvokeActivityAsync(...)`, fake orchestration runner/client/context types with sub-orchestrator, custom-status, and external-event support), `Sample.FunctionApp.Durable` isolates a minimal starter/orchestrator/activity app, and `Sample.FunctionApp.Durable.Tests` verifies metadata discovery, `[DurableClient]` starter execution, direct activity invocation, sub-orchestrator execution, custom-status visibility, external-event resume, and provider-driven orchestration completion fully in-process

## Goals

This framework aims to provide:
- **In-process testing**: No func.exe or external processes required
- **Fast execution**: Similar performance to ASP.NET Core TestServer
- **Two testing approaches**: gRPC-based (`FunctionsTestHost`) and ASP.NET Core pipeline-based (`FunctionsWebApplicationFactory`)
- **Full DI control**: Override services for testing
- **Middleware support**: Test middleware registered in `Program.cs`

## Approaches

### 1. FunctionsTestHost (gRPC-based)

Uses a custom gRPC host that mimics the Azure Functions host, starting the worker in-process and sending invocations directly via gRPC.

**Option A — Inline service registration** (override individual services for test doubles):

```csharp
_testHost = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunctions).Assembly)
    .ConfigureServices(services =>
    {
        // Register or override dependencies for testing
        services.AddSingleton<IMyService, MockMyService>();
    })
    .BuildAndStartAsync();
```

**Option B — Use `Program.CreateWorkerHostBuilder`** (inherit all app services automatically, `ConfigureFunctionsWorkerDefaults()` mode):

```csharp
// Program.cs — expose a worker-specific builder for gRPC non-WAF testing
public partial class Program
{
    public static IHostBuilder CreateWorkerHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureServices(ConfigureServices);

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IMyService, MyService>();
        // ... other registrations
    }
}

// Test — no need to re-register app services
_testHost = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunctions).Assembly)
    .WithHostBuilderFactory(Program.CreateWorkerHostBuilder)
    .BuildAndStartAsync();
```

**Option C — Use `Program.CreateHostBuilder`** (inherit all app services automatically, `ConfigureFunctionsWebApplication()` ASP.NET Core integration mode):

```csharp
// Program.cs — the standard CreateHostBuilder uses ConfigureFunctionsWebApplication()
public partial class Program
{
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWebApplication()
            .ConfigureServices(ConfigureServices);
}

// Test — framework auto-detects ASP.NET Core mode and routes HTTP requests
// to the worker's Kestrel server instead of dispatching over gRPC directly
_testHost = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunctions).Assembly)
    .WithHostBuilderFactory(Program.CreateHostBuilder)
    .BuildAndStartAsync();
```

> ℹ️ The framework auto-detects which mode is in use. With `ConfigureFunctionsWorkerDefaults()`, HTTP requests are dispatched via the gRPC `InvocationRequest` channel. With `ConfigureFunctionsWebApplication()`, the framework starts the worker's internal Kestrel server on an ephemeral port and routes `HttpClient` requests there.

**Middleware example** — `Sample.FunctionApp.Worker` registers `CorrelationIdMiddleware` from `Program.cs`. The middleware reads `x-correlation-id`, stores it in `FunctionContext.Items`, and the sample `/api/correlation` function exposes the value so both `FunctionsTestHost` and `FunctionsWebApplicationFactory` tests can assert it end-to-end.

**Service access + configuration overrides** — `FunctionsTestHost.Services` now exposes the worker DI container after startup, and `FunctionsTestHostBuilder.ConfigureSetting("Demo:Message", "configured-value")` lets tests overlay configuration values that functions can read through `IConfiguration`.

**Optional shared-host pattern** — if a gRPC test class can safely reset mutable app state between tests, it can amortize worker startup with an `IClassFixture`. See `tests/Sample.FunctionApp.Worker.Tests\SharedFunctionsTestHostFixture.cs` and `FunctionsTestHostReuseFixtureTests.cs` for a concrete example.

### 2. FunctionsWebApplicationFactory (ASP.NET Core pipeline)

Uses `WebApplicationFactory<TProgram>` directly, running the full ASP.NET Core pipeline from `Program.cs` — including custom middleware and services — through `TestServer`. Requires the function app to use `ConfigureFunctionsWebApplication()`.

> ✅ **Status**: Fully functional for all HTTP methods (GET, POST, PUT, DELETE) and `WithWebHostBuilder` DI overrides.

**Setup** — add to `Program.cs`:
```csharp
public partial class Program
{
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWebApplication()
            .ConfigureServices(services => { /* ... */ });
}

await Program.CreateHostBuilder(args).Build().RunAsync();
```

**Usage** (recommended: shared factory fixture + per-test state reset):
```csharp
// Reference: AzureFunctions.TestFramework.AspNetCore
public sealed class MyFunctionFactoryFixture : IAsyncLifetime, IDisposable
{
    public FunctionsWebApplicationFactory<Program> Factory { get; private set; } = default!;

    public Task InitializeAsync()
    {
        Factory = new FunctionsWebApplicationFactory<Program>();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Factory.DisposeAsync().AsTask();

    public void Dispose() => Factory.DisposeAsync().AsTask().GetAwaiter().GetResult();
}

public class MyFunctionTests
    : IClassFixture<MyFunctionFactoryFixture>, IAsyncLifetime
{
    private readonly MyFunctionFactoryFixture _fixture;
    private HttpClient? _client;

    public MyFunctionTests(MyFunctionFactoryFixture fixture)
        => _fixture = fixture;

    public Task InitializeAsync()
    {
        // Reset any shared singleton state before each test.
        if (_fixture.Factory.Services.GetService(typeof(IMyService)) is MyInMemoryService svc)
            svc.Reset();
        _client = _fixture.Factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Health_ReturnsOk()
        => (await _client!.GetAsync("/api/health")).EnsureSuccessStatusCode();
}
```

`FunctionsWebApplicationFactory.WithWebHostBuilder(...)` returns another `FunctionsWebApplicationFactory<TProgram>` instance rather than the base `WebApplicationFactory<TProgram>` clone path, so overridden-service scenarios keep the framework's custom startup/disposal behavior.

### 3. Timer Trigger Invocation

Use the `AzureFunctions.TestFramework.Timer` package to invoke timer-triggered functions directly from tests.

```csharp
// Reference: AzureFunctions.TestFramework.Timer
using AzureFunctions.TestFramework.Timer;
using Microsoft.Azure.Functions.Worker;

public class TimerFunctionTests : IAsyncLifetime
{
    private IFunctionsTestHost _testHost;

    public async Task InitializeAsync()
    {
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(MyTimerFunction).Assembly)
            .BuildAndStartAsync();
    }

    [Fact]
    public async Task HeartbeatTimer_RunsSuccessfully()
    {
        // Invoke with default TimerInfo (IsPastDue = false)
        var result = await _testHost.InvokeTimerAsync("HeartbeatTimer");
        Assert.True(result.Success);
    }

    [Fact]
    public async Task HeartbeatTimer_WhenPastDue_RunsSuccessfully()
    {
        var timerInfo = new TimerInfo { IsPastDue = true };
        var result = await _testHost.InvokeTimerAsync("HeartbeatTimer", timerInfo);
        Assert.True(result.Success);
    }

    public async Task DisposeAsync()
    {
        await _testHost.StopAsync();
        _testHost.Dispose();
    }
}
```

### 4. Service Bus Trigger Invocation

Use the `AzureFunctions.TestFramework.ServiceBus` package to invoke Service Bus–triggered functions directly from tests.

```csharp
// Reference: AzureFunctions.TestFramework.ServiceBus
using AzureFunctions.TestFramework.ServiceBus;
using Azure.Messaging.ServiceBus;

public class ServiceBusFunctionTests : IAsyncLifetime
{
    private IFunctionsTestHost _testHost;

    public async Task InitializeAsync()
    {
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(MyServiceBusFunction).Assembly)
            .BuildAndStartAsync();
    }

    [Fact]
    public async Task ProcessMessage_WithStringBody_Succeeds()
    {
        var message = new ServiceBusMessage("Hello from test!");
        var result = await _testHost.InvokeServiceBusAsync("ProcessOrderMessage", message);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task ProcessMessage_WithJsonBody_Succeeds()
    {
        var message = new ServiceBusMessage("{\"orderId\": \"abc123\"}")
        {
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString()
        };
        var result = await _testHost.InvokeServiceBusAsync("ProcessOrderMessage", message);
        Assert.True(result.Success);
    }

    public async Task DisposeAsync()
    {
        await _testHost.StopAsync();
        _testHost.Dispose();
    }
}
```

### 5. Blob Trigger Invocation

Use the `AzureFunctions.TestFramework.Blob` package to invoke blob-triggered functions directly from tests.

```csharp
// Reference: AzureFunctions.TestFramework.Blob
using AzureFunctions.TestFramework.Blob;

public class BlobFunctionTests : IAsyncLifetime
{
    private IFunctionsTestHost _testHost;

    public async Task InitializeAsync()
    {
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(MyBlobFunction).Assembly)
            .BuildAndStartAsync();
    }

    [Fact]
    public async Task ProcessBlob_WithTextContent_Succeeds()
    {
        var content = BinaryData.FromString("Hello from blob!");
        var result = await _testHost.InvokeBlobAsync("ProcessBlob", content, blobName: "test/file.txt");
        Assert.True(result.Success);
    }

    [Fact]
    public async Task ProcessBlob_WithJsonContent_Succeeds()
    {
        var content = BinaryData.FromObjectAsJson(new { id = "123", name = "test" });
        var result = await _testHost.InvokeBlobAsync(
            "ProcessBlob", content,
            blobName: "orders/order-123.json",
            containerName: "orders");
        Assert.True(result.Success);
    }

    public async Task DisposeAsync()
    {
        await _testHost.StopAsync();
        _testHost.Dispose();
    }
}
```

### 6. Event Grid Trigger Invocation

Use the `AzureFunctions.TestFramework.EventGrid` package to invoke Event Grid–triggered functions directly from tests. Both `EventGridEvent` (EventGrid schema) and `CloudEvent` (CloudEvents schema) are supported.

```csharp
// Reference: AzureFunctions.TestFramework.EventGrid
using AzureFunctions.TestFramework.EventGrid;
using Azure.Messaging.EventGrid;
using Azure.Messaging;

public class EventGridFunctionTests : IAsyncLifetime
{
    private IFunctionsTestHost _testHost;

    public async Task InitializeAsync()
    {
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(MyEventGridFunction).Assembly)
            .BuildAndStartAsync();
    }

    [Fact]
    public async Task ProcessEvent_WithEventGridEvent_Succeeds()
    {
        var eventGridEvent = new EventGridEvent(
            subject: "orders/order-123",
            eventType: "Order.Created",
            dataVersion: "1.0",
            data: BinaryData.FromObjectAsJson(new { orderId = "123" }));

        var result = await _testHost.InvokeEventGridAsync("ProcessOrderEvent", eventGridEvent);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task ProcessEvent_WithCloudEvent_Succeeds()
    {
        var cloudEvent = new CloudEvent(
            source: "/orders",
            type: "order.created",
            jsonSerializableData: new { orderId = "123" });

        var result = await _testHost.InvokeEventGridAsync("ProcessOrderEvent", cloudEvent);
        Assert.True(result.Success);
    }

    public async Task DisposeAsync()
    {
        await _testHost.StopAsync();
        _testHost.Dispose();
    }
}
```

### 7. Durable Functions spike

There is now a separate durable-only spike track:

- `src\AzureFunctions.TestFramework.Durable` — fake durable registration + runner/client/context helpers for in-process starter/orchestrator/activity testing
- `samples\Sample.FunctionApp.Durable` — isolated-worker durable sample with an HTTP starter, orchestrator, and activity
- `tests\Sample.FunctionApp.Durable.Tests` — fully in-process spike tests

Current spike result:

- function metadata discovery **works**
- fake orchestration scheduling and activity execution **work fully in-process**
- Azure-style `[DurableClient] DurableTaskClient` injection works under `FunctionsTestHost`
- direct activity invocation works through `IFunctionsTestHost.InvokeActivityAsync<TResult>(...)`
- fake sub-orchestrator execution works under `TaskOrchestrationContext.CallSubOrchestratorAsync<TResult>()`
- fake orchestration custom status flows through `SetCustomStatus(...)`, `OrchestrationMetadata.ReadCustomStatusAs<T>()`, and the durable HTTP status helpers
- fake orchestration external events work through `TaskOrchestrationContext.WaitForExternalEvent<T>()` and `DurableTaskClient.RaiseEventAsync(...)` when the orchestrator is already waiting
- tests can resolve `FunctionsDurableClientProvider` from `FunctionsTestHost.Services` and assert completed orchestration output
- the sample's HTTP starters now return JSON/plain response bodies that the direct gRPC path maps correctly under `FunctionsTestHost`
- the HTTP response mapper now falls back to plain invocation return values when an HTTP-trigger function does not return `HttpResponseData`
- the host synthesizes the Durable binding payload expected by the official Durable converter and preloads the extension's internal client-provider cache with the fake client

## Project Structure

```  
src/
  AzureFunctions.TestFramework.Core/       # gRPC host, worker hosting, HTTP invocation (net8.0;net9.0;net10.0)
  AzureFunctions.TestFramework.AspNetCore/ # WebApplicationFactory-based testing (net8.0;net9.0;net10.0)
  AzureFunctions.TestFramework.Http/       # HTTP-specific functionality (placeholder) (net8.0;net9.0;net10.0)
  AzureFunctions.TestFramework.Timer/      # TimerTrigger invocation support (net8.0;net9.0;net10.0)
  AzureFunctions.TestFramework.ServiceBus/ # ServiceBusTrigger invocation support (net8.0;net9.0;net10.0)
  AzureFunctions.TestFramework.Queue/      # QueueTrigger invocation support (net8.0;net9.0;net10.0)
  AzureFunctions.TestFramework.Blob/       # BlobTrigger invocation support (net8.0;net9.0;net10.0)
  AzureFunctions.TestFramework.EventGrid/  # EventGridTrigger invocation support (net8.0;net9.0;net10.0)
  AzureFunctions.TestFramework.Durable/    # Fake durable support for in-process starter/orchestrator/activity tests (net8.0;net9.0;net10.0)

samples/
  Sample.FunctionApp.Worker/              # Worker SDK 2.x sample (net9.0, TodoAPI + middleware + configuration endpoint + triggers)
  Sample.FunctionApp.Durable/             # Durable Functions spike sample (net9.0, HTTP starter + orchestrator + activity)

tests/
  Sample.FunctionApp.Worker.Tests/                 # gRPC-based tests (Worker SDK 2.x / net9.0)
  Sample.FunctionApp.Worker.WAF.Tests/             # WAF tests (Worker SDK 2.x / net9.0)
  Sample.FunctionApp.Durable.Tests/                # Durable spike tests (fully in-process / net9.0)
```

## Building

```bash
dotnet restore
dotnet build
```

## Testing

```bash
# All tests
dotnet test

# Worker SDK 2.x gRPC tests (.NET 9)
dotnet test tests/Sample.FunctionApp.Worker.Tests

# Worker SDK 2.x WebApplicationFactory tests (.NET 9)
dotnet test tests/Sample.FunctionApp.Worker.WAF.Tests

# Durable Functions spike tests (.NET 9)
dotnet test tests/Sample.FunctionApp.Durable.Tests

# Single test with detailed logging
dotnet test --filter "GetTodos_ReturnsEmptyList" --logger "console;verbosity=detailed"
```

## Known Issues

Existing HTTP/timer/queue/service-bus/blob/event-grid paths have no current blockers. The durable spike also runs fully in-process now with starter/orchestrator/activity/sub-orchestrator coverage and direct activity helpers. See [KNOWN_ISSUES.md](KNOWN_ISSUES.md) for the current status.

## References

- [Azure Functions Worker SDK](https://github.com/Azure/azure-functions-dotnet-worker)
- [Azure Functions RPC Protocol](https://github.com/Azure/azure-functions-language-worker-protobuf)
- [ASP.NET Core WebApplicationFactory](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests) (inspiration)

## License

MIT

