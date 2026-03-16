# Azure Functions Test Framework

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

An integration testing framework for Azure Functions (dotnet-isolated) that provides a TestServer/WebApplicationFactory-like experience.

## ⚠️ Project Status: Early Development

**Current Status**: Both testing approaches are fully functional for **Worker SDK 2.x (.NET 9)**. The gRPC-based `FunctionsTestHost` supports full CRUD HTTP invocations, timer/queue/service-bus trigger invocations, function metadata discovery, service-provider access via `Services`, configuration overrides via `ConfigureSetting`, and `WithHostBuilderFactory` supporting both `ConfigureFunctionsWorkerDefaults()` and `ConfigureFunctionsWebApplication()` modes. `FunctionsWebApplicationFactory` supports full CRUD including POST/PUT/DELETE and `WithWebHostBuilder` service overrides. The sample app now includes `CorrelationIdMiddleware`, configuration endpoints, and an opt-in shared-host gRPC fixture example. Startup/readiness is event-driven instead of delay/poll based, and the direct gRPC request path now precompiles route templates and reuses handlers per host. The Worker test projects are currently green (`15/15` gRPC tests and `6/6` WAF tests passing). All framework libraries target `net8.0;net9.0;net10.0` and are published as NuGet packages to NuGet.org (versioned via MinVer from git tags). See [KNOWN_ISSUES.md](KNOWN_ISSUES.md) for details.

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
- Tests run in parallel (xUnit `parallelizeTestCollections`) and in isolation (per-test host via `IAsyncLifetime` for gRPC tests; shared `IClassFixture` factory + per-test `InMemoryTodoService.Reset()` for WAF tests)
- gRPC EventStream shuts down gracefully on test teardown (no connection-abort errors, no Kestrel 5 s shutdown wait)
- **TimerTrigger invocations** via `AzureFunctions.TestFramework.Timer` package (`InvokeTimerAsync` extension method)
- **ServiceBus trigger invocations** via `AzureFunctions.TestFramework.ServiceBus` package (`InvokeServiceBusAsync` extension method)
- **Queue trigger invocations** via `AzureFunctions.TestFramework.Queue` package (`InvokeQueueAsync` extension method)
- **Function metadata discovery** via `IFunctionInvoker.GetFunctions()` — returns `IReadOnlyDictionary<string, IFunctionMetadata>` with name, function ID, entry point, script file, and raw binding JSON
- **`WithHostBuilderFactory`** — non-WAF gRPC tests can reuse the app's own `Program.CreateWorkerHostBuilder` or `Program.CreateHostBuilder`, automatically inheriting all registered services without re-registering them in each test. Both `ConfigureFunctionsWorkerDefaults()` and `ConfigureFunctionsWebApplication()` are supported.
- **`FunctionsTestHost.Services`** — access the worker's configured service provider after startup to resolve singletons and inspect test state directly
- **Configuration overrides** — `FunctionsTestHostBuilder.ConfigureSetting(key, value)` overlays test-specific configuration values into the worker host
- **Middleware sample + testing** — `Sample.FunctionApp.Worker` registers a `CorrelationIdMiddleware` that copies `x-correlation-id` into `FunctionContext.Items`, and both Worker test projects assert it through `/api/correlation`
- **Performance improvements** — startup waits now use worker connection/function-load signals instead of fixed delays, direct gRPC route matching is precompiled per host, and `CreateHttpClient()` reuses host-local handlers

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

**Usage** (recommended: shared factory + per-test state reset):
```csharp
// Reference: AzureFunctions.TestFramework.AspNetCore
public class MyFunctionTests
    : IClassFixture<FunctionsWebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly FunctionsWebApplicationFactory<Program> _factory;
    private HttpClient? _client;

    public MyFunctionTests(FunctionsWebApplicationFactory<Program> factory)
        => _factory = factory;

    public Task InitializeAsync()
    {
        // Reset any shared singleton state before each test.
        if (_factory.Services.GetService(typeof(IMyService)) is MyInMemoryService svc)
            svc.Reset();
        _client = _factory.CreateClient();
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

```
src/
  AzureFunctions.TestFramework.Core/       # gRPC host, worker hosting, HTTP invocation (net8.0;net9.0;net10.0)
  AzureFunctions.TestFramework.AspNetCore/ # WebApplicationFactory-based testing (net8.0;net9.0;net10.0)
  AzureFunctions.TestFramework.Http/       # HTTP-specific functionality (placeholder) (net8.0;net9.0;net10.0)
  AzureFunctions.TestFramework.Timer/      # TimerTrigger invocation support (net8.0;net9.0;net10.0)
  AzureFunctions.TestFramework.ServiceBus/ # ServiceBusTrigger invocation support (net8.0;net9.0;net10.0)
  AzureFunctions.TestFramework.Queue/      # QueueTrigger invocation support (net8.0;net9.0;net10.0)

samples/
  Sample.FunctionApp.Worker/              # Worker SDK 2.x sample (net9.0, TodoAPI + middleware + configuration endpoint + triggers)

tests/
  Sample.FunctionApp.Worker.Tests/                 # gRPC-based tests (Worker SDK 2.x / net9.0)
  Sample.FunctionApp.Worker.WAF.Tests/             # WAF tests (Worker SDK 2.x / net9.0)
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

# Single test with detailed logging
dotnet test --filter "GetTodos_ReturnsEmptyList" --logger "console;verbosity=detailed"
```

## Known Issues

No current blockers. See [KNOWN_ISSUES.md](KNOWN_ISSUES.md) for resolved issues and future enhancement ideas.

## References

- [Azure Functions Worker SDK](https://github.com/Azure/azure-functions-dotnet-worker)
- [Azure Functions RPC Protocol](https://github.com/Azure/azure-functions-language-worker-protobuf)
- [ASP.NET Core WebApplicationFactory](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests) (inspiration)

## License

MIT

