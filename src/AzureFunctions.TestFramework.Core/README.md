# AzureFunctions.TestFramework.Core

[![NuGet](https://img.shields.io/nuget/v/AzureFunctions.TestFramework.Core.svg)](https://www.nuget.org/packages/AzureFunctions.TestFramework.Core)

Core package for the Azure Functions Test Framework. Provides a `TestServer`/`WebApplicationFactory`-like in-process test host (`FunctionsTestHost`) for dotnet-isolated Azure Functions workers — no `func.exe`, no external processes, no open TCP ports.

Under the hood the framework uses ASP.NET Core's `TestServer` for both the gRPC communication channel and the worker's HTTP server.

## Project setup

### ASP.NET Core shared framework reference

If your function app uses `ConfigureFunctionsWebApplication()` (i.e., it references `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore`), it must declare a framework reference to `Microsoft.AspNetCore.App`:

```xml
<!-- YourFunctionApp.csproj -->
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
```

`AzureFunctions.TestFramework.Core` already declares this framework reference so that ASP.NET Core types are always resolved from the **shared runtime** in both the function app and the test framework. Without consistent framework resolution, `HttpContextConverter` cannot read `HttpRequest` from `FunctionContext` — the `as HttpContext` cast silently returns `null` due to a type identity mismatch between two physical copies of ASP.NET Core assemblies.

> ℹ️ You do **not** need to add `FrameworkReference` to your test project manually; it is propagated through the NuGet package metadata.

## Usage

### FunctionsTestHost — direct gRPC mode

Uses a custom gRPC host that mimics the Azure Functions host, starting the worker in-process and dispatching requests directly via the gRPC `InvocationRequest` channel. Works with `ConfigureFunctionsWorkerDefaults()`.

**Option A — Inline service registration** (override individual services for test doubles):

```csharp
_testHost = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunctions).Assembly)
    .ConfigureServices(services =>
    {
        services.AddSingleton<IMyService, MockMyService>();
    })
    .BuildAndStartAsync();
```

**Option B — `IHostBuilder` / `Program.CreateWorkerHostBuilder`** (inherit all app services automatically):

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
    }
}

// Test — no need to re-register app services
_testHost = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunctions).Assembly)
    .WithHostBuilderFactory(Program.CreateWorkerHostBuilder)
    .BuildAndStartAsync();
```

**Option C — `FunctionsApplicationBuilder` / `Program.CreateApplicationBuilder`** (modern `FunctionsApplication.CreateBuilder()` startup style):

```csharp
// Program.cs
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting; // required for UseMiddleware<T>()

public partial class Program
{
    public static FunctionsApplicationBuilder CreateApplicationBuilder(string[] args)
    {
        var builder = FunctionsApplication.CreateBuilder(args);
        builder.UseMiddleware<CorrelationMiddleware>();
        builder.Services.AddSingleton<IMyService, MyService>();
        return builder;
    }
}

// Test
_testHost = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunctions).Assembly)
    .WithHostApplicationBuilderFactory(Program.CreateApplicationBuilder)
    .BuildAndStartAsync();
```

> ⚠️ `UseMiddleware<T>()` is an extension method from `MiddlewareWorkerApplicationBuilderExtensions` in the `Microsoft.Extensions.Hosting` namespace. Add `using Microsoft.Extensions.Hosting;` to any file that calls it on a `FunctionsApplicationBuilder`.

### FunctionsTestHost — ASP.NET Core integration mode

The same `FunctionsTestHost` automatically detects when the worker uses `ConfigureFunctionsWebApplication()` and routes `HttpClient` requests to the worker's in-memory `TestServer` instead of dispatching over gRPC. The full ASP.NET Core middleware pipeline runs, and all ASP.NET Core-native binding types (`HttpRequest`, `FunctionContext`, typed route params, `CancellationToken`) work correctly. No TCP port is opened — the worker's Kestrel `IServer` is replaced with `TestServer` at startup.

**IHostBuilder style:**

```csharp
// Program.cs
public partial class Program
{
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWebApplication()
            .ConfigureServices(ConfigureServices);
}

// Test — framework auto-detects ASP.NET Core mode and routes requests to in-memory TestServer
_testHost = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunctions).Assembly)
    .WithHostBuilderFactory(Program.CreateHostBuilder)
    .BuildAndStartAsync();
```

**FunctionsApplicationBuilder style:**

```csharp
// Program.cs
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

public partial class Program
{
    public static FunctionsApplicationBuilder CreateWebApplicationBuilder(string[] args)
    {
        var builder = FunctionsApplication.CreateBuilder(args);
        builder.ConfigureFunctionsWebApplication(); // call before UseMiddleware
        builder.UseMiddleware<CorrelationMiddleware>();
        builder.Services.AddSingleton<IMyService, MyService>();
        return builder;
    }
}

// Test
_testHost = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunctions).Assembly)
    .WithHostApplicationBuilderFactory(Program.CreateWebApplicationBuilder)
    .BuildAndStartAsync();
```

> ℹ️ The framework auto-detects which mode is in use. With `ConfigureFunctionsWorkerDefaults()`, HTTP requests are dispatched via the gRPC `InvocationRequest` channel. With `ConfigureFunctionsWebApplication()`, the framework replaces the worker's Kestrel `IServer` with an in-memory `TestServer` — no TCP port is opened.

**Service overrides** — `ConfigureServices` on `FunctionsTestHostBuilder` lets tests swap out any registered service regardless of mode:

```csharp
_testHost = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunctions).Assembly)
    .WithHostBuilderFactory(Program.CreateHostBuilder)
    .ConfigureServices(services =>
    {
        services.RemoveAll<IMyService>();
        services.AddSingleton<IMyService>(new MockMyService());
    })
    .BuildAndStartAsync();
```

**Service access + configuration overrides** — `FunctionsTestHost.Services` exposes the worker DI container after startup, and `FunctionsTestHostBuilder.ConfigureSetting("Demo:Message", "configured-value")` lets tests overlay configuration values.

**Optional shared-host pattern** — if a test class can safely reset mutable app state between tests, it can amortize worker startup with an `IClassFixture`. See the sample projects for a concrete example.

## Worker-side Logging

By default the framework suppresses the worker host's logging below `Warning`. To see `ILogger` output from inside function code in test results, use `ConfigureWorkerLogging`:

**xUnit:**

```csharp
public async ValueTask InitializeAsync()
{
    var loggerProvider = new XUnitLoggerProvider(_output);
    _host = await new FunctionsTestHostBuilder()
        .WithFunctionsAssembly(typeof(MyFunctions).Assembly)
        .WithLoggerFactory(LoggerFactory.Create(b => b.AddProvider(loggerProvider)))
        .ConfigureWorkerLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Information);
            logging.AddProvider(loggerProvider);
        })
        .BuildAndStartAsync();
}
```

**NUnit:**

```csharp
[SetUp]
public async Task SetUp()
{
    var loggerProvider = new NUnitLoggerProvider();
    _host = await new FunctionsTestHostBuilder()
        .WithFunctionsAssembly(typeof(MyFunctions).Assembly)
        .WithLoggerFactory(LoggerFactory.Create(b => b.AddProvider(loggerProvider)))
        .ConfigureWorkerLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Information);
            logging.AddProvider(loggerProvider);
        })
        .BuildAndStartAsync();
}
```

**Key points:**
- `WithLoggerFactory` controls framework infrastructure logs (gRPC, host lifecycle, etc.)
- `ConfigureWorkerLogging` controls the worker host's logging pipeline — this is where function-side `ILogger` calls flow
- Category-specific filters override the global minimum: `logging.AddFilter("MyApp.Functions", LogLevel.Debug)`
- The same `ILoggerProvider` instance can be shared between both

## Architecture & Design Decisions

### Assembly Load Context (ALC) isolation prevention

**Problem:** The Worker SDK's `DefaultMethodInfoLocator.GetMethod()` calls `AssemblyLoadContext.Default.LoadFromAssemblyPath()` during `FunctionLoadRequest` processing. When the worker runs in-process (same process as the test runner), this can load a **second copy** of the same assembly, creating two distinct `RuntimeTypeHandle` values for the same type name. The SDK's built-in converters use `context.TargetType == typeof(T)` checks that silently fail, leaving trigger parameters null.

**Solution (three layers):**

1. **Root fix — `InProcessMethodInfoLocator`:** Replaces the SDK's internal `IMethodInfoLocator` via `DispatchProxy`. Searches `AppDomain.CurrentDomain.GetAssemblies()` for already-loaded assemblies instead of calling `LoadFromAssemblyPath`. Registered with `AddSingleton` (not `TryAdd`) so it wins over the SDK's `TryAddSingleton`.
2. **Defense-in-depth — `TestFunctionContextConverter` + `TestHttpRequestConverter`:** Registered at converter index 0. These compare types by `FullName` strings (immune to dual-load) and use reflection to access properties (bypassing `is T` casts).
3. **Build-time — `<FrameworkReference Include="Microsoft.AspNetCore.App" />`:** Declared in the Core csproj so ASP.NET Core types always resolve from the shared runtime.

### Function ID resolution

`GeneratedFunctionMetadataProvider` computes a stable hash for each function (`Name` + `ScriptFile` + `EntryPoint`). `GrpcHostService` stores the **hash-based** `FunctionId` from `FunctionMetadataResponse` — not the GUID from `FunctionLoadRequest` — because the worker's internal `_functionMap` uses the hash.

### GrpcWorker.StopAsync() is a no-op

The Azure Functions worker SDK's `GrpcWorker.StopAsync()` returns `Task.CompletedTask` immediately — it does **not** close the gRPC channel. `FunctionsTestHost` calls `_grpcHostService.SignalShutdownAsync()` before stopping `_grpcServerManager` to gracefully end the EventStream so `TestServer` can stop instantly.

### Durable converter interception

When using `ConfigureFunctionsWebApplication()`, the ASP.NET Core path does not send synthetic durable binding data in `InputData`. The real `DurableTaskClientConverter` receives null/empty `context.Source` and `[ConverterFallbackBehavior(Disallow)]` blocks fallback. The framework fixes this by registering `FakeDurableTaskClientInputConverter` in DI **as the service for the real `DurableTaskClientConverter` type**, so `ActivatorUtilities.GetServiceOrCreateInstance` returns the fake converter instead.

### IAutoConfigureStartup scanning

The functions assembly contains source-generated classes (`FunctionMetadataProviderAutoStartup`, `FunctionExecutorAutoStartup`) implementing `IAutoConfigureStartup`. `WorkerHostService` scans for and invokes these to register `GeneratedFunctionMetadataProvider` and `DirectFunctionExecutor`, overriding the defaults that would require a `functions.metadata` file on disk.

### Binding cache clearing (FunctionsApplicationBuilder + ASP.NET Core)

**Problem:** The Worker SDK's `IBindingCache<ConversionResult>` uses binding-name-only keys. If user middleware calls `GetHttpRequestDataAsync()` before `FunctionExecutionMiddleware` runs, the cache stores a `GrpcHttpRequestData` under key `"req"`. Later, `FunctionExecutionMiddleware` calls `BindFunctionInputAsync` for the same `"req"` binding with `TargetType=HttpRequest` — the cache returns the stale `GrpcHttpRequestData` and the SDK casts `(HttpRequest)GrpcHttpRequestData` → `InvalidCastException`.

**Solution:** In the `FunctionsApplicationBuilder` path, `WorkerHostService` appends a cache-clearing middleware that checks `context.Items.ContainsKey("HttpRequestContext")` — confirming ASP.NET Core mode is active — and uses `BindingCacheCleaner.TryClearBindingCache(context.InstanceServices)` to clear the internal `ConcurrentDictionary` via reflection. Converters then re-run with correct state.

> ℹ️ In the `IHostBuilder` path, the cache-poisoning scenario is avoided as long as `ConfigureFunctionsWebApplication()` is called **before** any `UseMiddleware<T>()` calls.

### Custom route prefix auto-detection

`FunctionsTestHostBuilder.Build()` reads `extensions.http.routePrefix` from the functions assembly's `host.json`. The prefix is used by `FunctionsHttpMessageHandler` (to strip it when matching routes) and by `FunctionsTestHost` (to set `HttpClient.BaseAddress`). This makes custom route prefixes work transparently without test-side configuration.

## References

- [Azure Functions Worker SDK](https://github.com/Azure/azure-functions-dotnet-worker)
- [Azure Functions RPC Protocol](https://github.com/Azure/azure-functions-language-worker-protobuf)
- [Full documentation](https://github.com/bjorkstromm/azure-functions-test-framework)

## License

MIT
