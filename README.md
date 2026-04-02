# Azure Functions Test Framework

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![NuGet](https://img.shields.io/nuget/v/AzureFunctions.TestFramework.Core.svg)](https://www.nuget.org/packages/AzureFunctions.TestFramework.Core)

An integration testing framework for Azure Functions (dotnet-isolated) that provides a TestServer/WebApplicationFactory-like experience.

## Project Status: Preview (pre-1.0)

`FunctionsTestHost` — the single unified test host — is **fully functional** for the Worker SDK 2.x (.NET 10) samples and test suites. It supports both **direct gRPC mode** (`ConfigureFunctionsWorkerDefaults()`) and **ASP.NET Core / Kestrel mode** (`ConfigureFunctionsWebApplication()`). No active blockers.

### Capabilities

| Area | Status |
|------|--------|
| **HTTP invocation** (GET / POST / PUT / PATCH / DELETE / HEAD / OPTIONS) | ✅ Both direct gRPC and ASP.NET Core / Kestrel modes |
| **Trigger packages** (Timer, Queue, ServiceBus, Blob, EventGrid) | ✅ Extension methods + result capture |
| **Durable Functions** (starter, orchestrator, activity, sub-orchestrator, external events) | ✅ Fake-backed in-process |
| **ASP.NET Core integration** (`ConfigureFunctionsWebApplication`) | ✅ Full parameter binding incl. `HttpRequest`, `FunctionContext`, typed route params, `CancellationToken` |
| **`WithHostBuilderFactory` + `ConfigureServices`** | ✅ DI overrides, inherited app services |
| **Custom route prefixes** | ✅ Auto-detected from `host.json` |
| **Middleware testing** | ✅ End-to-end in both modes |
| **Output binding capture** | ✅ `ReadReturnValueAs<T>()`, `ReadOutputAs<T>(bindingName)` |
| **Service access / configuration overrides** | ✅ `Services`, `ConfigureSetting`, `ConfigureEnvironmentVariable` |
| **Metadata discovery** | ✅ `IFunctionInvoker.GetFunctions()` |
| **NuGet packaging** | ✅ `net8.0;net10.0`, Source Link, symbol packages, central package management |
| **CI** | ✅ xUnit + NUnit, push + PR |

## Goals

This framework aims to provide:
- **In-process testing**: No func.exe or external processes required
- **Fast execution**: Similar performance to ASP.NET Core TestServer
- **Single unified test host**: `FunctionsTestHost` handles both direct gRPC mode and ASP.NET Core / Kestrel mode
- **Full DI control**: Override services for testing
- **Middleware support**: Test middleware registered in `Program.cs`

## NuGet package map

The shipping package set is currently:

- `AzureFunctions.TestFramework.Core` — gRPC-based in-process test host, HTTP client path (both direct gRPC and ASP.NET Core / Kestrel modes), metadata inspection, and shared invocation result types
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

This is the standard requirement for Azure Functions apps that use ASP.NET Core integration. `AzureFunctions.TestFramework.Core` also declares this framework reference so that ASP.NET Core types are always resolved from the **shared runtime** in both the function app and the test framework. Without consistent framework resolution, `HttpContextConverter` cannot read `HttpRequest` from `FunctionContext` — the `as HttpContext` cast silently returns `null` due to a type identity mismatch between two physical copies of ASP.NET Core assemblies.

> ℹ️ You do **not** need to add `FrameworkReference` to your test project manually; it is propagated through the test framework's NuGet package metadata.

## Common commands

```bash
# Build solution
dotnet build --configuration Release

# Run all tests
dotnet test --configuration Release

# Worker SDK 2.x tests — direct gRPC mode (xUnit)
dotnet test tests/Sample.FunctionApp.Worker.Tests --no-build --configuration Release

# Worker SDK 2.x tests — NUnit
dotnet test tests/Sample.FunctionApp.Worker.NUnit.Tests --no-build --configuration Release

# Durable Functions tests
dotnet test tests/Sample.FunctionApp.Durable.Tests --no-build --configuration Release

# Custom route prefix tests
dotnet test tests/Sample.FunctionApp.CustomRoutePrefix.Tests --no-build --configuration Release
dotnet test tests/Sample.FunctionApp.CustomRoutePrefix.AspNetCore.Tests --no-build --configuration Release

# Pack NuGet packages locally
dotnet pack --configuration Release --output ./artifacts
```

## Next likely areas

- Richer durable lifecycle helpers (terminate/suspend/resume and more management helpers)
- Additional typed helpers for more complex output payloads
- More middleware scenarios such as authorization and exception handling
- More binding types such as Event Hubs, Cosmos DB, and SignalR

## Approaches

### 1. FunctionsTestHost — direct gRPC mode

Uses a custom gRPC host that mimics the Azure Functions host, starting the worker in-process and dispatching HTTP requests directly via the gRPC `InvocationRequest` channel. Works with `ConfigureFunctionsWorkerDefaults()`.

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

**Option B — Use `Program.CreateWorkerHostBuilder`** (inherit all app services automatically):

```csharp
// Program.cs — expose a worker-specific builder for gRPC mode testing
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

### 2. FunctionsTestHost — ASP.NET Core / Kestrel mode

The same `FunctionsTestHost` automatically detects when the worker uses `ConfigureFunctionsWebApplication()` and routes `HttpClient` requests to the worker's real Kestrel server instead of dispatching over gRPC. The full ASP.NET Core middleware pipeline runs, and all ASP.NET Core-native binding types (`HttpRequest`, `FunctionContext`, Guid route params, `CancellationToken`) work correctly.

```csharp
// Program.cs — expose a host builder for ASP.NET Core / Kestrel mode testing
public partial class Program
{
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWebApplication()
            .ConfigureServices(ConfigureServices);
}

// Test — framework auto-detects ASP.NET Core mode and routes requests to Kestrel
_testHost = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunctions).Assembly)
    .WithHostBuilderFactory(Program.CreateHostBuilder)
    .BuildAndStartAsync();
```

> ℹ️ The framework auto-detects which mode is in use. With `ConfigureFunctionsWorkerDefaults()`, HTTP requests are dispatched via the gRPC `InvocationRequest` channel. With `ConfigureFunctionsWebApplication()`, the framework starts the worker's internal Kestrel server on an ephemeral port and routes `HttpClient` requests there.

**Service overrides in either mode** — `ConfigureServices` on `FunctionsTestHostBuilder` lets tests swap out any registered service regardless of which mode is used:

```csharp
_testHost = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunctions).Assembly)
    .WithHostBuilderFactory(Program.CreateHostBuilder)   // or CreateWorkerHostBuilder
    .ConfigureServices(services =>
    {
        services.RemoveAll<IMyService>();
        services.AddSingleton<IMyService>(new MockMyService());
    })
    .BuildAndStartAsync();
```

**Middleware example** — `Sample.FunctionApp.Worker` registers `CorrelationIdMiddleware` from `Program.cs`. The middleware reads `x-correlation-id`, stores it in `FunctionContext.Items`, and the sample `/api/correlation` function exposes the value. Tests in both modes assert the middleware end-to-end.

**Service access + configuration overrides** — `FunctionsTestHost.Services` exposes the worker DI container after startup, and `FunctionsTestHostBuilder.ConfigureSetting("Demo:Message", "configured-value")` lets tests overlay configuration values that functions can read through `IConfiguration`.

**Optional shared-host pattern** — if a test class can safely reset mutable app state between tests, it can amortize worker startup with an `IClassFixture`. See `tests/Sample.FunctionApp.Worker.Tests/SharedFunctionsTestHostFixture.cs` and `FunctionsTestHostReuseFixtureTests.cs` for a concrete example.

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
3. **Build-time — `<FrameworkReference Include="Microsoft.AspNetCore.App" />`:** Declared in `Core` csproj so ASP.NET Core types always resolve from the shared runtime, not from NuGet packages. Without this, two physical copies of `Microsoft.AspNetCore.Http.Abstractions.dll` load and `HttpContextConverter.GetHttpContext()` returns null.

### Function ID resolution

`GeneratedFunctionMetadataProvider` computes a stable hash for each function (`Name` + `ScriptFile` + `EntryPoint`). `GrpcHostService` stores the **hash-based** `FunctionId` from `FunctionMetadataResponse` — not the GUID from `FunctionLoadRequest` — because the worker's internal `_functionMap` uses the hash. Sending the wrong ID causes "function not found" at invocation time.

### GrpcWorker.StopAsync() is a no-op

The Azure Functions worker SDK's `GrpcWorker.StopAsync()` returns `Task.CompletedTask` immediately — it does **not** close the gRPC channel. `FunctionsTestHost` calls `_grpcHostService.SignalShutdownAsync()` before stopping `_grpcServerManager` to gracefully end the EventStream so Kestrel can stop instantly (no 5 s `ShutdownTimeout` wait).

### Durable converter interception

When using `ConfigureFunctionsWebApplication()`, the ASP.NET Core middleware path does not send synthetic durable binding data in `InputData` (unlike the gRPC-direct path). The real `DurableTaskClientConverter` receives null/empty `context.Source`, returns `ConversionResult.Failed()`, and `[ConverterFallbackBehavior(Disallow)]` on `DurableClientAttribute` blocks fallback. The framework fixes this by registering `FakeDurableTaskClientInputConverter` in DI **as the service for the real `DurableTaskClientConverter` type**, so `ActivatorUtilities.GetServiceOrCreateInstance` returns our fake converter instead of creating the real one.

### IAutoConfigureStartup scanning

The functions assembly contains source-generated classes (`FunctionMetadataProviderAutoStartup`, `FunctionExecutorAutoStartup`) implementing `IAutoConfigureStartup`. `WorkerHostService` scans for and invokes these to register `GeneratedFunctionMetadataProvider` and `DirectFunctionExecutor`, overriding the defaults that would require a `functions.metadata` file on disk.

### Custom route prefix auto-detection

`FunctionsTestHostBuilder.Build()` reads `extensions.http.routePrefix` from the functions assembly's `host.json`. The prefix is used by `FunctionsHttpMessageHandler` (to strip it when matching routes) and by `FunctionsTestHost` (to set `HttpClient.BaseAddress`). This makes custom route prefixes work transparently.

## Project Structure

```  
src/
  AzureFunctions.TestFramework.Core/         # gRPC host, worker hosting, HTTP invocation — both modes (net8.0;net10.0)
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
  Sample.FunctionApp.Worker.Tests/                         # xUnit — both direct gRPC and ASP.NET Core / Kestrel mode (Worker SDK 2.x)
  Sample.FunctionApp.Worker.NUnit.Tests/                   # NUnit — both direct gRPC and ASP.NET Core / Kestrel mode (Worker SDK 2.x)
  Sample.FunctionApp.Durable.Tests/                        # xUnit — Durable Functions (fully in-process)
  Sample.FunctionApp.CustomRoutePrefix.Tests/              # xUnit — custom prefix via direct gRPC (ConfigureFunctionsWorkerDefaults)
  Sample.FunctionApp.CustomRoutePrefix.AspNetCore.Tests/   # xUnit — custom prefix via ASP.NET Core / Kestrel mode (ConfigureFunctionsWebApplication)
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

# Worker SDK 2.x tests (xUnit) — direct gRPC and ASP.NET Core / Kestrel mode
dotnet test tests/Sample.FunctionApp.Worker.Tests

# Worker SDK 2.x tests (NUnit) — direct gRPC and ASP.NET Core / Kestrel mode
dotnet test tests/Sample.FunctionApp.Worker.NUnit.Tests

# Durable Functions tests
dotnet test tests/Sample.FunctionApp.Durable.Tests

# Custom route prefix tests
dotnet test tests/Sample.FunctionApp.CustomRoutePrefix.Tests
dotnet test tests/Sample.FunctionApp.CustomRoutePrefix.AspNetCore.Tests

# Single test with detailed logging
dotnet test --filter "GetTodos_ReturnsEmptyList" --logger "console;verbosity=detailed"
```

## Known Issues

See [KNOWN_ISSUES.md](https://github.com/bjorkstromm/azure-functions-test-framework/blob/main/KNOWN_ISSUES.md) for active caveats.

## References

- [Azure Functions Worker SDK](https://github.com/Azure/azure-functions-dotnet-worker)
- [Azure Functions RPC Protocol](https://github.com/Azure/azure-functions-language-worker-protobuf)

## License

MIT

