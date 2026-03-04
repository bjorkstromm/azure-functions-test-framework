# Azure Functions Test Framework

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

An integration testing framework for Azure Functions (dotnet-isolated) that provides a TestServer/WebApplicationFactory-like experience.

## ⚠️ Project Status: Early Development

**Current Status**: Both testing approaches are fully functional. The gRPC-based `FunctionsTestHost` supports full CRUD HTTP invocations, timer/queue/service-bus trigger invocations, function metadata discovery, and `WithHostBuilderFactory` supporting both `ConfigureFunctionsWorkerDefaults()` and `ConfigureFunctionsWebApplication()` modes (30/30 tests pass). `FunctionsWebApplicationFactory` supports full CRUD including POST/PUT/DELETE and `WithWebHostBuilder` service overrides (4/4 tests pass). See [KNOWN_ISSUES.md](KNOWN_ISSUES.md) for details.

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



```
src/
  AzureFunctions.TestFramework.Core/       # gRPC host, worker hosting, HTTP invocation
  AzureFunctions.TestFramework.AspNetCore/ # WebApplicationFactory-based testing
  AzureFunctions.TestFramework.Http/       # HTTP-specific functionality (placeholder)
  
samples/
  Sample.FunctionApp/                      # Example Azure Functions app (TodoAPI)
  
tests/
  Sample.FunctionApp.Tests/                         # gRPC-based integration tests (FunctionsTestHost)
  Sample.FunctionApp.WebApplicationFactory.Tests/   # WebApplicationFactory-based integration tests
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

# gRPC-based tests only
dotnet test tests/Sample.FunctionApp.Tests

# WebApplicationFactory-based tests only
dotnet test tests/Sample.FunctionApp.WebApplicationFactory.Tests

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

