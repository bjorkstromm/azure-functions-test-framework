# Azure Functions Test Framework

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

An integration testing framework for Azure Functions (dotnet-isolated) that provides a TestServer/WebApplicationFactory-like experience.

## Project Status: Preview (pre-1.0)

All testing approaches — gRPC-based `FunctionsTestHost`, ASP.NET Core `FunctionsWebApplicationFactory`, and non-HTTP trigger invocation — are **fully functional** for the Worker SDK 2.x (.NET 10) samples and test suites. No active blockers.

### Capabilities

| Area | Status |
|------|--------|
| **HTTP invocation** (GET / POST / PUT / DELETE) | ✅ Both gRPC and WAF paths |
| **Trigger packages** (Timer, Queue, ServiceBus, Blob, EventGrid) | ✅ Extension methods + result capture |
| **Durable Functions** (starter, orchestrator, activity, sub-orchestrator, external events) | ✅ Fake-backed in-process |
| **ASP.NET Core integration** (`ConfigureFunctionsWebApplication`) | ✅ Full parameter binding incl. `HttpRequest`, `FunctionContext`, typed route params, `CancellationToken` |
| **`WithWebHostBuilder` / `WithHostBuilderFactory`** | ✅ DI overrides, inherited app services |
| **Custom route prefixes** | ✅ Auto-detected from `host.json` |
| **Middleware testing** | ✅ End-to-end through both paths |
| **Output binding capture** | ✅ `ReadReturnValueAs<T>()`, `ReadOutputAs<T>(bindingName)` |
| **Service access / configuration overrides** | ✅ `Services`, `ConfigureSetting`, `ConfigureEnvironmentVariable` |
| **Metadata discovery** | ✅ `IFunctionInvoker.GetFunctions()` |
| **NuGet packaging** | ✅ `net8.0;net10.0`, Source Link, symbol packages, central package management |
| **CI** | ✅ xUnit + NUnit, push + PR |

## Goals

This framework aims to provide:
- **In-process testing**: No func.exe or external processes required
- **Fast execution**: Similar performance to ASP.NET Core TestServer
- **Two testing approaches**: gRPC-based (`FunctionsTestHost`) and ASP.NET Core pipeline-based (`FunctionsWebApplicationFactory`)
- **Full DI control**: Override services for testing
- **Middleware support**: Test middleware registered in `Program.cs`

## NuGet package map

The shipping package set is currently:

- `AzureFunctions.TestFramework.Core` — gRPC-based in-process test host, HTTP client path, metadata inspection, and shared invocation result types
- `AzureFunctions.TestFramework.Http.AspNetCore` — `FunctionsWebApplicationFactory<TProgram>` integration for ASP.NET Core pipeline testing
- `AzureFunctions.TestFramework.Http` — currently minimal HTTP-focused package kept in the published package set for future HTTP-specific helpers
- `AzureFunctions.TestFramework.Timer` — `InvokeTimerAsync(...)`
- `AzureFunctions.TestFramework.Queue` — `InvokeQueueAsync(...)`
- `AzureFunctions.TestFramework.ServiceBus` — `InvokeServiceBusAsync(...)`
- `AzureFunctions.TestFramework.Blob` — `InvokeBlobAsync(...)`
- `AzureFunctions.TestFramework.EventGrid` — `InvokeEventGridAsync(...)` for both `EventGridEvent` and `CloudEvent`
- `AzureFunctions.TestFramework.Durable` — fake-backed durable helpers including `ConfigureFakeDurableSupport(...)`, durable client/provider helpers, status helpers, and direct activity invocation

## Project setup requirements

### ASP.NET Core shared framework reference

If your function app uses `ConfigureFunctionsWebApplication()` (i.e., it references `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore`), it must declare a framework reference to `Microsoft.AspNetCore.App`:

```xml
<!-- YourFunctionApp.csproj -->
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
```

This is the standard requirement for Azure Functions apps that use ASP.NET Core integration. The test framework libraries (`AzureFunctions.TestFramework.Core`, `AzureFunctions.TestFramework.Http.AspNetCore`) also declare this framework reference so that ASP.NET Core types are always resolved from the **shared runtime** in both the function app and the test framework. Without consistent framework resolution, `HttpContextConverter` cannot read `HttpRequest` from `FunctionContext` — the `as HttpContext` cast silently returns `null` due to a type identity mismatch between two physical copies of ASP.NET Core assemblies.

> ℹ️ You do **not** need to add `FrameworkReference` to your test project manually; it is propagated through the test framework's NuGet package metadata.

## Common commands

```bash
# Build solution
dotnet build --configuration Release

# Run all tests
dotnet test --configuration Release

# Worker SDK 2.x gRPC tests (xUnit)
dotnet test tests/Sample.FunctionApp.Worker.Tests --no-build --configuration Release

# Worker SDK 2.x WAF tests (xUnit)
dotnet test tests/Sample.FunctionApp.Worker.WAF.Tests --no-build --configuration Release

# Durable Functions tests
dotnet test tests/Sample.FunctionApp.Durable.Tests --no-build --configuration Release

# Custom route prefix tests
dotnet test tests/Sample.FunctionApp.CustomRoutePrefix.Tests --no-build --configuration Release

# Pack NuGet packages locally
dotnet pack --configuration Release --output ./artifacts
```

## Next likely areas

- Richer durable lifecycle helpers (terminate/suspend/resume and more management helpers)
- Additional typed helpers for more complex output payloads
- More middleware scenarios such as authorization and exception handling
- More binding types such as Event Hubs, Cosmos DB, and SignalR

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

**Option C — Use `Program.CreateWorkerHostBuilder`** (inherit all app services automatically, `ConfigureFunctionsWebApplication()` ASP.NET Core integration mode):

```csharp
// Program.cs — expose a dedicated builder for FunctionsTestHost use
public partial class Program
{
    public static IHostBuilder CreateWorkerHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWebApplication()
            .ConfigureServices(ConfigureServices);
}

// Test — framework auto-detects ASP.NET Core mode and routes HTTP requests
// to the worker's Kestrel server instead of dispatching over gRPC directly.
// Functions that take HttpRequest, FunctionContext, Guid route params, and
// CancellationToken all work correctly in this mode.
_testHost = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunctions).Assembly)
    .WithHostBuilderFactory(Program.CreateWorkerHostBuilder)
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
// Reference: AzureFunctions.TestFramework.Http.AspNetCore
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

### 7. Durable Functions

The `AzureFunctions.TestFramework.Durable` package provides fake-backed durable support for in-process integration testing. No real Durable Task runtime is needed.

**Why fake-backed?** The real Durable Task execution engine relies on external storage (Azure Storage / Netherite / MSSQL) and the Durable Task Framework host, neither of which runs inside the test framework's in-process worker. Instead, `ConfigureFakeDurableSupport(...)` registers a `FakeDurableTaskClient` and companion types that intercept `[DurableClient]` binding resolution at the DI level, letting starter functions, orchestrators, activities, and sub-orchestrators execute fully in-process.

**Setup:**
```csharp
_testHost = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyDurableFunction).Assembly)
    .ConfigureFakeDurableSupport(provider =>
    {
        // Register orchestration runners
        provider.AddOrchestration<string>("MyOrchestrator", ctx => MyOrchestratorFunction.RunAsync(ctx));
    })
    .BuildAndStartAsync();
```

**Coverage:**
- `[DurableClient] DurableTaskClient` injection (both gRPC-direct and ASP.NET Core paths)
- Direct activity invocation via `IFunctionsTestHost.InvokeActivityAsync<TResult>(...)`
- Fake orchestration scheduling and activity execution
- Sub-orchestrator execution via `TaskOrchestrationContext.CallSubOrchestratorAsync<TResult>()`
- Custom status via `SetCustomStatus(...)` and `OrchestrationMetadata.ReadCustomStatusAs<T>()`
- External events: both wait-then-raise and buffered raise-before-wait flows
- `FunctionsDurableClientProvider` resolution from `FunctionsTestHost.Services`

## Architecture & Design Decisions

This section explains *why* certain non-obvious implementation choices were made.

### Assembly Load Context (ALC) isolation prevention

**Problem:** The Worker SDK's `DefaultMethodInfoLocator.GetMethod()` calls `AssemblyLoadContext.Default.LoadFromAssemblyPath()` during `FunctionLoadRequest` processing. When the worker runs in-process (same process as the test runner), this can load a **second copy** of the same assembly, creating two distinct `RuntimeTypeHandle` values for the same type name. The SDK's built-in converters use `context.TargetType == typeof(T)` checks that silently fail, leaving trigger parameters (`FunctionContext`, `HttpRequest`) null.

**Solution (three layers):**

1. **Root fix — `InProcessMethodInfoLocator`:** Replaces the SDK's internal `IMethodInfoLocator` via `DispatchProxy` (the interface is internal). Searches `AppDomain.CurrentDomain.GetAssemblies()` for already-loaded assemblies instead of calling `LoadFromAssemblyPath`. Registered with `AddSingleton` (not `TryAdd`) so it wins over the SDK's `TryAddSingleton`.
2. **Defense-in-depth — `TestFunctionContextConverter` + `TestHttpRequestConverter`:** Registered at converter index 0 via `PostConfigure<WorkerOptions>`. These compare types by `FullName` strings (immune to dual-load) and use reflection to access properties (bypassing `is T` casts).
3. **Build-time — `<FrameworkReference Include="Microsoft.AspNetCore.App" />`:** Declared in both `Core` and `Http.AspNetCore` csproj files so ASP.NET Core types always resolve from the shared runtime, not from NuGet packages. Without this, two physical copies of `Microsoft.AspNetCore.Http.Abstractions.dll` load and `HttpContextConverter.GetHttpContext()` returns null.

### Function ID resolution

`GeneratedFunctionMetadataProvider` computes a stable hash for each function (`Name` + `ScriptFile` + `EntryPoint`). `GrpcHostService` stores the **hash-based** `FunctionId` from `FunctionMetadataResponse` — not the GUID from `FunctionLoadRequest` — because the worker's internal `_functionMap` uses the hash. Sending the wrong ID causes "function not found" at invocation time.

### GrpcWorker.StopAsync() is a no-op

The Azure Functions worker SDK's `GrpcWorker.StopAsync()` returns `Task.CompletedTask` immediately — it does **not** close the gRPC channel. This matters in two places:

- **WAF disposal:** `FunctionsWebApplicationFactory.Dispose` calls `_grpcHostService.SignalShutdownAsync()` after `base.Dispose()` but before `_grpcServerManager.StopAsync()`. This gracefully ends the EventStream so Kestrel can stop instantly (no 5 s `ShutdownTimeout` wait).
- **`WithWebHostBuilder`:** `GrpcAwareHost` wraps the derived factory's host, cancels that EventStream, and waits for it to finish before calling `_inner.Dispose()` — preventing `ObjectDisposedException`.

### Durable converter interception

When using `ConfigureFunctionsWebApplication()`, the ASP.NET Core middleware path does not send synthetic durable binding data in `InputData` (unlike the gRPC-direct path). The real `DurableTaskClientConverter` receives null/empty `context.Source`, returns `ConversionResult.Failed()`, and `[ConverterFallbackBehavior(Disallow)]` on `DurableClientAttribute` blocks fallback. The framework fixes this by registering `FakeDurableTaskClientInputConverter` in DI **as the service for the real `DurableTaskClientConverter` type**, so `ActivatorUtilities.GetServiceOrCreateInstance` returns our fake converter instead of creating the real one.

### IAutoConfigureStartup scanning

The functions assembly contains source-generated classes (`FunctionMetadataProviderAutoStartup`, `FunctionExecutorAutoStartup`) implementing `IAutoConfigureStartup`. Both `WorkerHostService` and `FunctionsWebApplicationFactory.CreateHost` scan for and invoke these to register `GeneratedFunctionMetadataProvider` and `DirectFunctionExecutor`, overriding the defaults that would require a `functions.metadata` file on disk.

### Custom route prefix auto-detection

`FunctionsTestHostBuilder.Build()` reads `extensions.http.routePrefix` from the functions assembly's `host.json`. The prefix is used by `FunctionsHttpMessageHandler` (to strip it when matching routes) and by `FunctionsTestHost` (to set `HttpClient.BaseAddress`). This makes custom route prefixes work transparently.

## Project Structure

```  
src/
  AzureFunctions.TestFramework.Core/         # gRPC host, worker hosting, HTTP invocation (net8.0;net10.0)
  AzureFunctions.TestFramework.Http.AspNetCore/ # WebApplicationFactory-based testing (net8.0;net10.0)
  AzureFunctions.TestFramework.Http/         # HTTP-specific functionality placeholder (net8.0;net10.0)
  AzureFunctions.TestFramework.Timer/        # TimerTrigger invocation (net8.0;net10.0)
  AzureFunctions.TestFramework.Queue/        # QueueTrigger invocation (net8.0;net10.0)
  AzureFunctions.TestFramework.ServiceBus/   # ServiceBusTrigger invocation (net8.0;net10.0)
  AzureFunctions.TestFramework.Blob/         # BlobTrigger invocation (net8.0;net10.0)
  AzureFunctions.TestFramework.EventGrid/    # EventGridTrigger invocation (net8.0;net10.0)
  AzureFunctions.TestFramework.Durable/      # Fake durable support (net8.0;net10.0)

samples/
  Sample.FunctionApp.Worker/                 # Worker SDK 2.x (net10.0) — TodoAPI, middleware, triggers
  Sample.FunctionApp.Durable/               # Durable Functions sample — HTTP starter + orchestrator + activity
  Sample.FunctionApp.CustomRoutePrefix/      # Custom route prefix with ConfigureFunctionsWorkerDefaults()
  Sample.FunctionApp.CustomRoutePrefix.AspNetCore/ # Custom route prefix with ConfigureFunctionsWebApplication()

tests/
  Sample.FunctionApp.Worker.Tests/           # xUnit — gRPC-based (Worker SDK 2.x)
  Sample.FunctionApp.Worker.WAF.Tests/       # xUnit — WAF-based (Worker SDK 2.x)
  Sample.FunctionApp.Worker.NUnit.Tests/     # NUnit — gRPC-based (Worker SDK 2.x)
  Sample.FunctionApp.Worker.WAF.NUnit.Tests/ # NUnit — WAF-based (Worker SDK 2.x)
  Sample.FunctionApp.Durable.Tests/          # xUnit — Durable Functions (fully in-process)
  Sample.FunctionApp.CustomRoutePrefix.Tests/ # xUnit — custom prefix via gRPC (WorkerDefaults)
  Sample.FunctionApp.CustomRoutePrefix.AspNetCore.Tests/ # xUnit — custom prefix via gRPC (AspNetCore)
  Sample.FunctionApp.CustomRoutePrefix.WAF.Tests/ # xUnit — custom prefix via WAF
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

# Worker SDK 2.x gRPC tests (xUnit)
dotnet test tests/Sample.FunctionApp.Worker.Tests

# Worker SDK 2.x WAF tests (xUnit)
dotnet test tests/Sample.FunctionApp.Worker.WAF.Tests

# NUnit variants
dotnet test tests/Sample.FunctionApp.Worker.NUnit.Tests
dotnet test tests/Sample.FunctionApp.Worker.WAF.NUnit.Tests

# Durable Functions tests
dotnet test tests/Sample.FunctionApp.Durable.Tests

# Custom route prefix tests
dotnet test tests/Sample.FunctionApp.CustomRoutePrefix.Tests
dotnet test tests/Sample.FunctionApp.CustomRoutePrefix.AspNetCore.Tests
dotnet test tests/Sample.FunctionApp.CustomRoutePrefix.WAF.Tests

# Single test with detailed logging
dotnet test --filter "GetTodos_ReturnsEmptyList" --logger "console;verbosity=detailed"
```

## Known Issues

See [KNOWN_ISSUES.md](https://github.com/bjorkstromm/azure-functions-test-framework/blob/main/KNOWN_ISSUES.md) for active caveats.

## References

- [Azure Functions Worker SDK](https://github.com/Azure/azure-functions-dotnet-worker)
- [Azure Functions RPC Protocol](https://github.com/Azure/azure-functions-language-worker-protobuf)
- [ASP.NET Core WebApplicationFactory](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests) (inspiration)

## License

MIT

