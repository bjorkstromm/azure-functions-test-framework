# Azure Functions Test Framework - Copilot Instructions

## Session Rules

> **Always update `README.md` and `KNOWN_ISSUES.md` at the end of every session** to reflect the current state of the project: what now works, what is still blocked, and what changed. These are the primary documentation files used to track progress between sessions.

## Project Overview
This is an integration testing framework for Azure Functions (dotnet-isolated) that provides a TestServer/WebApplicationFactory-like experience. It runs Azure Functions in-process without func.exe, using ASP.NET Core's `TestServer` for both the gRPC communication channel and the worker's HTTP server — no TCP ports are opened.

**Current Status**: `FunctionsTestHost` is **fully functional** for the current **Worker SDK 2.x (.NET 10)** samples and test suites. It supports both **direct gRPC mode** (`ConfigureFunctionsWorkerDefaults()`) and **ASP.NET Core integration mode** (`ConfigureFunctionsWebApplication()`), and works with both the classic `IHostBuilder` API and the modern `IHostApplicationBuilder` / `FunctionsApplicationBuilder` API (via `WithHostApplicationBuilderFactory`). Under the hood, the framework uses ASP.NET Core's `TestServer` for both the gRPC communication channel and the worker's HTTP server — no TCP ports are opened. Features include full CRUD, TimerTrigger, QueueTrigger, ServiceBusTrigger, BlobTrigger, EventGridTrigger, middleware assertions, `Services`, `ConfigureSetting()`, output binding capture, custom route prefixes, and service overrides via `ConfigureServices`. Startup/readiness is event-driven and the direct gRPC path precompiles route matching per host. All framework libraries target `net8.0;net10.0` and declare `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to prevent ASP.NET Core type-identity issues. Tests run in parallel and in isolation across xUnit, NUnit, and TUnit. No known blockers.

## Architecture

### Key Components
1. **AzureFunctions.TestFramework.Core**: Main framework
   - `FunctionsTestHost`: Orchestrates worker startup and gRPC communication
   - `GrpcHostService`: Implements Azure Functions host gRPC protocol (bidirectional streaming)
     - `FindFunctionId(method, path, routePrefix)`: Route matching with `{param}` support
     - `SendInvocationRequestAsync(invocationId, method, path)`: Fires InvocationRequest to worker; always includes an empty `RpcHttp` `ParameterBinding` for the HTTP trigger binding name (e.g. `"req"`) so `FunctionsHttpProxyingMiddleware.IsHttpTriggerFunction` correctly identifies the function and `IHttpCoordinator` coordination runs — without it `FunctionContext.Items["HttpRequestContext"]` is never populated
   - `GrpcServerManager`: Manages in-memory gRPC server lifecycle (backed by `TestServer`)
   - `WorkerHostService`: Starts Azure Functions Worker using HostBuilder or FunctionsApplicationBuilder (in-process); when ASP.NET Core integration is detected, replaces Kestrel's `IServer` with `TestServer` and exposes the in-memory `HttpMessageHandler`
   - `FunctionsHttpMessageHandler`: Custom HttpMessageHandler for intercepting HTTP requests
   - `HttpRequestMapper`/`HttpResponseMapper`: Convert between HTTP and gRPC messages

2. **AzureFunctions.TestFramework.Http**: HTTP client support (`CreateHttpClient()` extension), request/response mapping, and forwarding handlers for both direct gRPC and ASP.NET Core integration modes

3. **AzureFunctions.TestFramework.Timer**: TimerTrigger invocation support — depends on Core + `Microsoft.Azure.Functions.Worker.Extensions.Timer`. Exposes `InvokeTimerAsync(this IFunctionsTestHost, string functionName, TimerInfo? timerInfo = null)` extension method.

4. **AzureFunctions.TestFramework.Queue**: QueueTrigger invocation — `InvokeQueueAsync(this IFunctionsTestHost, string functionName, string message)`.

5. **AzureFunctions.TestFramework.ServiceBus**: ServiceBusTrigger invocation — `InvokeServiceBusAsync(this IFunctionsTestHost, string functionName, ServiceBusMessage message)`.

6. **AzureFunctions.TestFramework.Blob**: BlobTrigger invocation — `InvokeBlobAsync(this IFunctionsTestHost, string functionName, BinaryData content, ...)`.

7. **AzureFunctions.TestFramework.EventGrid**: EventGridTrigger invocation — `InvokeEventGridAsync(...)` supporting both `EventGridEvent` and `CloudEvent`.

8. **AzureFunctions.TestFramework.Durable**: Fake-backed durable support — `ConfigureFakeDurableSupport(...)`, `FakeDurableTaskClient`, `FakeDurableTaskClientInputConverter`, `FakeTaskOrchestrationContext`, `FakeDurableExternalEventHub`, `FunctionsDurableClientProvider`, and `InvokeActivityAsync<TResult>()`. Uses DI-based converter interception so `[DurableClient] DurableTaskClient` resolves in both gRPC-direct and ASP.NET Core paths.

9. **Sample.FunctionApp.Worker**: Example functions (TodoAPI with CRUD + HeartbeatTimerFunction + CorrelationIdMiddleware + output binding demos + Queue/ServiceBus/Blob/EventGrid triggers). Exposes `Program.CreateHostBuilder` (ASP.NET Core integration mode).

10. **Sample.FunctionApp**: Minimal worker app (net10.0) used by sample test projects (`Sample.FunctionApp.Tests.XUnit`, `.NUnit`, `.TUnit`).

11. **Sample.FunctionApp.Durable**: Durable Functions sample (HTTP starter + orchestrator + activity + sub-orchestrator + external events). Exposes `Program.CreateHostBuilder` (ASP.NET Core integration mode).

12. **Sample.FunctionApp.CustomRoutePrefix**: Custom route prefix sample using `ConfigureFunctionsWorkerDefaults()` with `host.json` `routePrefix: "v1"`

13. **Sample.FunctionApp.CustomRoutePrefix.AspNetCore**: Custom route prefix sample using `ConfigureFunctionsWebApplication()`.

14. **TestProject.HostBuilder**: Function app project for the `IHostBuilder` direct-gRPC test flavour. Exposes `Program.CreateWorkerHostBuilder`.

15. **TestProject.HostBuilder.AspNetCore**: Function app project for the `IHostBuilder` ASP.NET Core test flavour. Exposes `Program.CreateHostBuilder`.

16. **TestProject.HostApplicationBuilder**: Function app project for the `FunctionsApplicationBuilder` direct-gRPC test flavour. Exposes `Program.CreateApplicationBuilder`.

17. **TestProject.HostApplicationBuilder.AspNetCore**: Function app project for the `FunctionsApplicationBuilder` ASP.NET Core test flavour. Exposes `Program.CreateWebApplicationBuilder`.

18. **tests/Shared**: Shared test logic consumed by all test flavour projects.
    - `Shared/Functions/` — shared function implementations used by all test projects
    - `Shared/Tests/` — abstract base classes for all test categories (HTTP trigger, middleware, triggers, durable, custom route prefix, etc.)
    - `TestProject.Shared` — shared class library (services, models) referenced by the test function-app projects

19. **TestProject.HostBuilder.Tests**: xUnit tests — direct gRPC mode, `IHostBuilder`
20. **TestProject.HostBuilder.AspNetCore.Tests**: xUnit tests — ASP.NET Core integration mode, `IHostBuilder`
21. **TestProject.HostApplicationBuilder.Tests**: xUnit tests — direct gRPC mode, `FunctionsApplicationBuilder`
22. **TestProject.HostApplicationBuilder.AspNetCore.Tests**: xUnit tests — ASP.NET Core integration mode, `FunctionsApplicationBuilder`

23. **TestProject.CustomRoutePrefix.HostBuilder.Tests**: xUnit CRP tests — direct gRPC mode, `IHostBuilder`
24. **TestProject.CustomRoutePrefix.HostBuilder.AspNetCore.Tests**: xUnit CRP tests — ASP.NET Core integration mode, `IHostBuilder`
25. **TestProject.CustomRoutePrefix.HostApplicationBuilder.Tests**: xUnit CRP tests — direct gRPC mode, `FunctionsApplicationBuilder`
26. **TestProject.CustomRoutePrefix.HostApplicationBuilder.AspNetCore.Tests**: xUnit CRP tests — ASP.NET Core integration mode, `FunctionsApplicationBuilder`

27. **Sample.FunctionApp.Worker.Tests**: `FunctionsTestHost` integration tests covering ASP.NET Core integration mode (xUnit)

28. **Sample.FunctionApp.Durable.Tests**: Durable Functions integration tests (xUnit)

29. **Sample.FunctionApp.CustomRoutePrefix.Tests**: Custom route prefix tests via direct gRPC mode (WorkerDefaults, xUnit)

30. **Sample.FunctionApp.CustomRoutePrefix.AspNetCore.Tests**: Custom route prefix tests via ASP.NET Core integration mode (AspNetCore, xUnit)

### How It Works

#### FunctionsTestHost — two modes

**Mode 1: Direct gRPC** (`ConfigureFunctionsWorkerDefaults()`)
1. **Build Phase**: `FunctionsTestHostBuilder.Build()` creates GrpcServerManager and starts it with `TestServer` (in-memory, no TCP port)
2. **Startup Phase**: WorkerHostService creates HostBuilder, configures it to connect to gRPC server via an in-memory `HttpMessageHandler`; Worker's `StartAsync()` connects; GrpcHostService handles bidirectional streaming (EventStream RPC)
3. **Testing Phase**: HttpClient uses FunctionsHttpMessageHandler → converts HTTP request → gRPC InvocationRequest → worker executes → gRPC InvocationResponse → HTTP response

**Mode 2: ASP.NET Core integration** (`ConfigureFunctionsWebApplication()`)
1. Same startup as above; additionally, WorkerHostService replaces the worker's Kestrel `IServer` with `TestServer` (in-memory, no TCP port)
2. `WorkerHostService` detects ASP.NET Core integration and obtains the in-memory `HttpMessageHandler` from `TestServer`
3. `AspNetCoreForwardingHandler` routes HTTP requests through the in-memory handler instead of over gRPC
4. The full ASP.NET Core middleware pipeline runs; `HttpRequest`, `FunctionContext`, typed route params, and `CancellationToken` all bind correctly

The test writes use `WithHostBuilderFactory(Program.CreateHostBuilder)` for ASP.NET Core integration mode or `WithHostBuilderFactory(Program.CreateWorkerHostBuilder)` for direct gRPC mode. Alternatively, `WithHostApplicationBuilderFactory(Program.CreateApplicationBuilder)` or `WithHostApplicationBuilderFactory(Program.CreateWebApplicationBuilder)` can be used when the function app uses the modern `FunctionsApplication.CreateBuilder()` startup pattern. The mode is auto-detected.

#### Timer Trigger Invocation
1. **Function discovery**: `GrpcHostService` parses `timerTrigger` bindings during `HandleFunctionsMetadataResponse`, populating `_timerFunctionMap[functionName] = (FunctionId, ParameterName)`
2. **API**: `host.InvokeTimerAsync("HeartbeatTimer", timerInfo?)` (from `AzureFunctions.TestFramework.Timer`)
3. **Flow**: Extension method serializes `TimerInfo` → camelCase JSON, puts it at `context.InputData["$timerJson"]`, calls `host.Invoker.InvokeAsync` with `TriggerType = "timerTrigger"`; Core reads it back and calls `GrpcHostService.InvokeTimerFunctionAsync` which builds an `InvocationRequest` with the timer JSON as `ParameterBinding`

#### Non-HTTP Trigger Invocation (Queue, ServiceBus, Blob, EventGrid)
All trigger packages follow the same pattern through `IFunctionInvoker.InvokeAsync`:
1. **Extension method** (e.g. `InvokeQueueAsync`) serializes the trigger-specific payload and sets it on `InvocationContext.InputData`
2. **Core's `IFunctionInvoker`** reads the input data and calls `GrpcHostService` to build an `InvocationRequest` with the appropriate `ParameterBinding` for the trigger type
3. **Worker** deserializes the binding and executes the function
4. **Result** is captured in `FunctionInvocationResult`, which surfaces plain return values and output-binding payloads

#### Output Binding Capture
`FunctionInvocationResult` captures `ReturnValue` and `OutputData` from non-HTTP trigger invocations:
- `ReadReturnValueAs<T>()` — typed access to the function's return value
- `ReadOutputAs<T>(bindingName)` — typed access to named output bindings (queue, blob, table, etc.)

#### Durable Converter Interception
When using `ConfigureFunctionsWebApplication()`, the ASP.NET Core path does not send synthetic durable binding data in `InputData`. The real `DurableTaskClientConverter` gets null `context.Source` and `[ConverterFallbackBehavior(Disallow)]` blocks fallback. The framework registers `FakeDurableTaskClientInputConverter` in DI as the service for the real `DurableTaskClientConverter` type, so `ActivatorUtilities.GetServiceOrCreateInstance` returns our fake instead.

### Worker Configuration
The worker needs these configuration keys (set internally by WorkerHostService):
- `Functions:Worker:HostEndpoint` - gRPC server URI (placeholder; actual traffic is routed in-memory via `InMemoryGrpcClientFactory`)
- `Functions:Worker:WorkerId` - Unique GUID
- `Functions:Worker:RequestId` - Unique GUID  
- `Functions:Worker:GrpcMaxMessageLength` - "2147483647"

> ℹ️ The `HostEndpoint` URI is still required by the Worker SDK for configuration validation, but no TCP connection is made. The framework's `InMemoryGrpcClientFactory` intercepts gRPC client creation and routes all traffic through `TestServer.CreateHandler()` — a pure in-memory `HttpMessageHandler`.

### gRPC Protocol
Uses Azure Functions RPC protocol from `azure-functions-language-worker-protobuf`:
- **FunctionRpc.EventStream**: Bidirectional streaming RPC
- **Key messages**: StartStream, WorkerInitRequest/Response, FunctionsMetadataRequest/Response, FunctionLoadRequest/Response, InvocationRequest/Response

### IAutoConfigureStartup (Critical)
The functions assembly contains source-generated classes (`FunctionMetadataProviderAutoStartup`, `FunctionExecutorAutoStartup`) implementing `IAutoConfigureStartup`. `WorkerHostService` scans for and invokes these to register `GeneratedFunctionMetadataProvider` and `DirectFunctionExecutor`, overriding the defaults that would require a `functions.metadata` file.

### ALC (Assembly Load Context) Isolation Prevention
The Worker SDK's `DefaultMethodInfoLocator.GetMethod()` calls `AssemblyLoadContext.Default.LoadFromAssemblyPath()` during `FunctionLoadRequest` processing. In-process hosting (test runner + worker in same process) can cause the same assembly to be loaded twice, breaking type-identity checks (`typeof(T) ==`, `obj is T`). The framework prevents this at three layers:

1. **Root fix: `InProcessMethodInfoLocator`** — Replaces the SDK's internal `IMethodInfoLocator` via `DispatchProxy` (since the interface is internal). Searches `AppDomain.CurrentDomain.GetAssemblies()` for already-loaded assemblies instead of calling `LoadFromAssemblyPath`. Registered with `AddSingleton` (not `TryAdd`) so it wins over the SDK's `TryAddSingleton`. Falls back to `LoadFromAssemblyPath` only if the assembly isn't already loaded.

2. **Defense-in-depth: `TestFunctionContextConverter` + `TestHttpRequestConverter`** — Registered at converter index 0 via `PostConfigure<WorkerOptions>`, these compare by `FullName` strings (immune to dual-load) and use reflection to access properties (bypassing `is T` casts).

3. **Build-time: `<FrameworkReference Include="Microsoft.AspNetCore.App" />`** — Ensures ASP.NET Core types resolve from the shared framework, not from NuGet packages.

### Custom Route Prefix Auto-Detection
`FunctionsTestHostBuilder.Build()` reads `extensions.http.routePrefix` from the functions assembly's `host.json`. The prefix is used by `FunctionsHttpMessageHandler` (to strip it when matching routes) and by `FunctionsTestHost` (to set `HttpClient.BaseAddress`). This makes custom route prefixes (e.g. `"v1"`) work transparently without test-side configuration.

### Function ID Resolution
`GeneratedFunctionMetadataProvider` computes a stable hash for each function (`Name` + `ScriptFile` + `EntryPoint`). `GrpcHostService` stores the hash-based `FunctionId` from `FunctionMetadataResponse` in `_functionRouteToId` (not the GUID from `FunctionLoadRequest`), so `SendInvocationRequestAsync` sends the correct ID that matches the worker's internal `_functionMap`.

### GrpcWorker.StopAsync() is a No-Op
The Azure Functions worker SDK's `GrpcWorker.StopAsync()` returns `Task.CompletedTask` immediately — it does NOT close the gRPC channel. `FunctionsTestHost` calls `_grpcHostService.SignalShutdownAsync()` before stopping `_grpcServerManager` to gracefully end the EventStream so `TestServer` can stop instantly.

## Development Guidelines

### Testing Approach
```bash
# Build solution
dotnet build

# Run all tests
dotnet test

# 4-flavour test matrix (IHostBuilder / FunctionsApplicationBuilder × direct gRPC / ASP.NET Core)
dotnet test tests/TestProject.HostBuilder.Tests
dotnet test tests/TestProject.HostBuilder.AspNetCore.Tests
dotnet test tests/TestProject.HostApplicationBuilder.Tests
dotnet test tests/TestProject.HostApplicationBuilder.AspNetCore.Tests

# Custom route prefix tests (4-flavour)
dotnet test tests/TestProject.CustomRoutePrefix.HostBuilder.Tests
dotnet test tests/TestProject.CustomRoutePrefix.HostBuilder.AspNetCore.Tests
dotnet test tests/TestProject.CustomRoutePrefix.HostApplicationBuilder.Tests
dotnet test tests/TestProject.CustomRoutePrefix.HostApplicationBuilder.AspNetCore.Tests

# Worker SDK 2.x sample tests (xUnit)
dotnet test samples/Sample.FunctionApp.Worker.Tests

# Durable Functions tests
dotnet test samples/Sample.FunctionApp.Durable.Tests

# Custom route prefix sample tests
dotnet test samples/Sample.FunctionApp.CustomRoutePrefix.Tests
dotnet test samples/Sample.FunctionApp.CustomRoutePrefix.AspNetCore.Tests

# Run single test
dotnet test samples/Sample.FunctionApp.Worker.Tests --filter "GetTodos_ReturnsEmptyList" --logger "console;verbosity=detailed"
```

### Code Style
- Use nullable reference types
- Add XML documentation for public APIs
- Follow existing patterns in GrpcHostService for async message handling
- Don't block the gRPC event stream (use Task.Run for long-running operations)

### Testing Conventions
- xUnit tests use `IAsyncLifetime` per-test (each test gets its own `FunctionsTestHost`)
- NUnit tests use `[SetUp]`/`[TearDown]` for per-test host lifecycle
- Shared-host patterns use `IClassFixture` (xUnit) or `[OneTimeSetUp]` (NUnit) + per-test state reset
- New features must be tested across all four flavours: **direct gRPC × IHostBuilder**, **direct gRPC × FunctionsApplicationBuilder**, **ASP.NET Core × IHostBuilder**, **ASP.NET Core × FunctionsApplicationBuilder**. Shared test logic lives in `tests/Shared/Tests/` as abstract base classes consumed by each flavour's test project.
- One test project references exactly one function app project. CRP (custom route prefix) test projects are separate from main test projects.
- Each function app project's `Program.cs` exposes a single builder factory method (no multi-builder files).
- `UseMiddleware<T>()` on `FunctionsApplicationBuilder` requires `using Microsoft.Extensions.Hosting;` — it is an extension method from `MiddlewareWorkerApplicationBuilderExtensions` in that namespace

## Project Structure
```
src/
  AzureFunctions.TestFramework.Core/
    Core abstractions, in-memory gRPC server (TestServer-backed), worker hosting — both direct gRPC and ASP.NET Core integration modes
    ├── Grpc/
    │   ├── GrpcHostService.cs         # Bidirectional streaming handler + route matching
    │   ├── GrpcServerManager.cs       # In-memory gRPC server lifecycle (TestServer)
    │   ├── InMemoryWorkerClientFactory.cs  # Routes Worker SDK gRPC traffic through TestServer (no TCP)
    │   └── GrpcLoggingInterceptor.cs  # Logging middleware
    ├── Worker/
    │   ├── WorkerHostService.cs             # In-process worker hosting (IHostBuilder + FunctionsApplicationBuilder; replaces Kestrel with TestServer)
    │   ├── InProcessMethodInfoLocator.cs    # DispatchProxy-based IMethodInfoLocator replacement
    │   └── Converters/
    │       ├── TestFunctionContextConverter.cs  # ALC defense-in-depth
    │       └── TestHttpRequestConverter.cs      # ALC defense-in-depth
    ├── Protos/
    │   └── FunctionRpc.proto          # Azure Functions RPC protocol
    ├── FunctionsTestHost.cs           # Main orchestrator
    ├── FunctionsTestHostBuilder.cs    # Fluent builder API (WithHostBuilderFactory + WithHostApplicationBuilderFactory)
    └── IFunctionsTestHostBuilder.cs   # Builder interface
    
  AzureFunctions.TestFramework.Http/
    HTTP client support, request/response mapping, and forwarding handlers
    ├── FunctionsTestHostHttpExtensions.cs  # CreateHttpClient() extension — auto-selects gRPC or TestServer handler
    ├── FunctionsHttpMessageHandler.cs      # Routes requests via gRPC InvocationRequest (direct gRPC mode)
    ├── AspNetCoreForwardingHandler.cs      # Routes requests via in-memory TestServer (ASP.NET Core integration mode)
    ├── HttpRequestMapper.cs               # HTTP → gRPC conversion
    └── HttpResponseMapper.cs              # gRPC → HTTP conversion
    
  AzureFunctions.TestFramework.Timer/        # InvokeTimerAsync extension
  AzureFunctions.TestFramework.Queue/        # InvokeQueueAsync extension
  AzureFunctions.TestFramework.ServiceBus/   # InvokeServiceBusAsync extension
  AzureFunctions.TestFramework.Blob/         # InvokeBlobAsync extension
  AzureFunctions.TestFramework.EventGrid/    # InvokeEventGridAsync extension
  AzureFunctions.TestFramework.Durable/      # Fake durable support (converter, client, context, runner, events)
    
samples/
  Sample.FunctionApp/
    Minimal worker app (net10.0) — used by sample test projects
  Sample.FunctionApp.Tests.XUnit/            # xUnit sample test project
  Sample.FunctionApp.Tests.NUnit/            # NUnit sample test project
  Sample.FunctionApp.Tests.TUnit/            # TUnit sample test project
  Sample.FunctionApp.Worker/
    Worker SDK 2.x sample (net10.0) — TodoAPI, middleware, triggers, output bindings
    CreateHostBuilder = ASP.NET Core integration mode
  Sample.FunctionApp.Worker.Tests/           # xUnit sample integration tests
  Sample.FunctionApp.Durable/
    Durable Functions sample (net10.0) — HTTP starter + orchestrator + activity + sub-orchestrator
    CreateHostBuilder = ASP.NET Core integration mode
  Sample.FunctionApp.Durable.Tests/          # xUnit Durable sample tests
  Sample.FunctionApp.CustomRoutePrefix/
    Custom route prefix sample (net10.0) — ConfigureFunctionsWorkerDefaults() + host.json routePrefix "v1"
  Sample.FunctionApp.CustomRoutePrefix.Tests/              # xUnit CRP sample tests (gRPC)
  Sample.FunctionApp.CustomRoutePrefix.AspNetCore/
    Custom route prefix sample (net10.0) — ConfigureFunctionsWebApplication()
  Sample.FunctionApp.CustomRoutePrefix.AspNetCore.Tests/   # xUnit CRP sample tests (ASP.NET Core)
    
tests/
  # 4-flavour test matrix — shared logic in tests/Shared/
  TestProject.HostBuilder/
    Function app — IHostBuilder, ConfigureFunctionsWorkerDefaults()
    CreateWorkerHostBuilder = direct gRPC mode
  TestProject.HostBuilder.Tests/              # xUnit — direct gRPC, IHostBuilder
  TestProject.HostBuilder.AspNetCore/
    Function app — IHostBuilder, ConfigureFunctionsWebApplication()
    CreateHostBuilder = ASP.NET Core integration mode
  TestProject.HostBuilder.AspNetCore.Tests/   # xUnit — ASP.NET Core integration, IHostBuilder
  TestProject.HostApplicationBuilder/
    Function app — FunctionsApplicationBuilder, ConfigureFunctionsWorkerDefaults()
    CreateApplicationBuilder = direct gRPC mode
  TestProject.HostApplicationBuilder.Tests/   # xUnit — direct gRPC, FunctionsApplicationBuilder
  TestProject.HostApplicationBuilder.AspNetCore/
    Function app — FunctionsApplicationBuilder, ConfigureFunctionsWebApplication()
    CreateWebApplicationBuilder = ASP.NET Core integration mode
  TestProject.HostApplicationBuilder.AspNetCore.Tests/  # xUnit — ASP.NET Core integration, FunctionsApplicationBuilder
  # Custom route prefix 4-flavour matrix (one test project per CRP function app)
  TestProject.CustomRoutePrefix.HostBuilder/            # CRP — IHostBuilder, gRPC
  TestProject.CustomRoutePrefix.HostBuilder.Tests/
  TestProject.CustomRoutePrefix.HostBuilder.AspNetCore/            # CRP — IHostBuilder, ASP.NET Core
  TestProject.CustomRoutePrefix.HostBuilder.AspNetCore.Tests/
  TestProject.CustomRoutePrefix.HostApplicationBuilder/            # CRP — FunctionsApplicationBuilder, gRPC
  TestProject.CustomRoutePrefix.HostApplicationBuilder.Tests/
  TestProject.CustomRoutePrefix.HostApplicationBuilder.AspNetCore/ # CRP — FunctionsApplicationBuilder, ASP.NET Core
  TestProject.CustomRoutePrefix.HostApplicationBuilder.AspNetCore.Tests/
  Shared/
    Functions/   # Shared function implementations for the 4-flavour matrix
    Tests/       # Abstract base classes for all test categories
  TestProject.Shared/   # Shared class library (services, models) consumed by test projects
```

## References
- Azure Functions Worker: https://github.com/Azure/azure-functions-dotnet-worker
- RPC Protocol: https://github.com/Azure/azure-functions-language-worker-protobuf
- Worker Configuration: See WorkerHostBuilderExtensions.cs in azure-functions-dotnet-worker

## Success Metrics
✅ Solution builds successfully (net8.0 / net10.0)
✅ Worker starts in-process using HostBuilder
✅ Worker starts in-process using FunctionsApplicationBuilder (IHostApplicationBuilder)
✅ Worker connects to gRPC server
✅ gRPC bidirectional streaming works
✅ Function loading/discovery
✅ Function invocation works (FunctionsTestHost — all HTTP methods + all trigger types)
✅ All FunctionsTestHost integration tests pass (xUnit + NUnit + TUnit)
✅ Direct gRPC mode: full CRUD, middleware, service overrides, configuration, output bindings
✅ ASP.NET Core integration mode: full CRUD, HttpRequest, FunctionContext, typed route params, CancellationToken, middleware, service overrides
✅ IHostBuilder and FunctionsApplicationBuilder both supported across direct gRPC and ASP.NET Core modes (4-flavour matrix)
✅ Tests run in parallel and in isolation (xUnit parallelizeTestCollections + IAsyncLifetime / NUnit SetUp+TearDown)
✅ Graceful gRPC EventStream shutdown (no connection-abort errors)
✅ CI workflow runs on pull requests and pushes to main (xUnit + NUnit suites)
✅ Current sample targets Worker SDK 2.x (2.51.0)
✅ All framework libraries target net8.0;net10.0
✅ Custom route prefix auto-detection from host.json
✅ Output binding capture (queue, blob, table)
✅ Durable Functions (starter, orchestrator, activity, sub-orchestrator, external events)
✅ NuGet packaging with MinVer, Source Link, symbol packages
✅ Worker-side logging configurable via `ConfigureWorkerLogging` (routes function `ILogger` output to test output)
