# Known Issues and Current Status

## 🟢 What Works

### Core Infrastructure ✅
- Solution structure with 8 projects builds successfully (6 framework libs + 1 sample app + 2 test suites)
- All NuGet dependencies resolve correctly
- gRPC protocol definitions integrated from azure-functions-language-worker-protobuf
- All framework libraries target `net8.0;net9.0;net10.0`
- Worker SDK 2.x (2.51.0) exclusively — Worker SDK 1.x support dropped
- NuGet packages produced via `dotnet pack` using MinVer versioning from git tags; published to NuGet.org via GitHub Actions `publish.yml` workflow on `v*.*.*` tag push

### Worker Hosting ✅
- Azure Functions Worker starts in-process using HostBuilder
- No external processes required (no func.exe, no dotnet exec)
- Worker connects successfully to our gRPC server via localhost
- Bidirectional gRPC streaming (EventStream) functional
- Proper port coordination between server and worker
- Source-generated `IAutoConfigureStartup` implementations (`GeneratedFunctionMetadataProvider`, `DirectFunctionExecutor`) are automatically registered from the functions assembly

### gRPC Communication ✅
- GrpcServerManager starts Kestrel with HTTP/2 on ephemeral port
- GrpcHostService implements FunctionRpc bidirectional streaming
- Worker connects and sends StartStream message
- WorkerInitRequest/Response exchange successful (using TaskCompletionSource, not Task.Delay)
- FunctionsMetadataRequest/Response returns all sample functions
- FunctionLoadRequest/Response succeeds for all sample functions
- Logging and interceptor infrastructure working

### Function Loading/Discovery ✅
- `FUNCTIONS_APPLICATION_DIRECTORY` env var set to function assembly directory
- `FunctionAppDirectory` in WorkerInitRequest set correctly
- Worker discovers all functions via its source-generated `GeneratedFunctionMetadataProvider`
- All functions loaded successfully before `StartAsync` returns
- `FunctionsTestHost.StartAsync` awaits `WaitForFunctionsLoadedAsync()` to ensure full readiness

### HTTP Client API ✅
- FunctionsTestHostBuilder with fluent API
- `CreateHttpClient()` returns HttpClient with custom handler
- FunctionsHttpMessageHandler intercepts HTTP requests
- Dynamic route map (keyed as `"METHOD:route"`) built from function metadata — no more hardcoded routes
- HttpRequestMapper converts HTTP → gRPC InvocationRequest (includes required TraceContext)
- HttpResponseMapper converts gRPC InvocationResponse → HTTP response (bytes decoded as UTF-8)

### Test Infrastructure ✅
- Sample function app with 9 HTTP endpoints (Todo CRUD + Health + Echo + Correlation + Configuration), 1 HeartbeatTimer, 1 ServiceBus trigger, and 1 Queue trigger in `Sample.FunctionApp.Worker` (`net9.0`)
- **Worker SDK 2.x (net9.0)**: 15 integration tests in `Sample.FunctionApp.Worker.Tests` pass (FunctionsTestHost-based)
- **Worker SDK 2.x (net9.0)**: 6 integration tests in `Sample.FunctionApp.Worker.WAF.Tests` pass (FunctionsWebApplicationFactory-based)
- `CorrelationIdMiddleware` is covered end-to-end in both test projects; the `FunctionsTestHost` sample uses `WithHostBuilderFactory(Program.CreateHostBuilder)` and the WAF sample uses `FunctionsWebApplicationFactory<Program>`
- `FunctionsTestHost.Services` exposes the worker service provider after startup
- `FunctionsTestHostBuilder.ConfigureSetting()` overlays test-specific configuration values that functions can consume via `IConfiguration`
- Dedicated `FunctionsTestHost` tests now verify both inline service replacement and `WithHostBuilderFactory(Program.CreateWorkerHostBuilder)` service overrides
- `FunctionsTestHost` startup and `FunctionsWebApplicationFactory` readiness are event-driven (worker connection + function-load signals) rather than fixed-delay polling
- Direct gRPC HTTP dispatch precompiles route templates once per host, and `FunctionsTestHost.CreateHttpClient()` reuses host-local handlers
- `Sample.FunctionApp.Worker.Tests\SharedFunctionsTestHostFixture.cs` demonstrates an opt-in shared-host pattern for suites that can reset state between tests

### FunctionsWebApplicationFactory ✅
- `GrpcInvocationBridgeStartupFilter` fires an `InvocationRequest` for every incoming HTTP request, unblocking `WorkerRequestServicesMiddleware`
- `InvocationIdStartupFilter` injects `x-ms-invocation-id` header when absent
- `GrpcHostService.FindFunctionId()` matches routes with `{param}` placeholder support
- Host startup completes in ~0.5 s — no longer hangs
- All HTTP methods (GET, POST, PUT, DELETE) pass end-to-end
- `WithWebHostBuilder` service overrides work — secondary worker EventStream ends cleanly before host DI is disposed

### FunctionsTestHost — ASP.NET Core Integration Mode ✅
- `WithHostBuilderFactory(Program.CreateHostBuilder)` now works with `ConfigureFunctionsWebApplication()` in non-WAF mode
- The framework auto-detects ASP.NET Core integration by checking for `IServer` in the worker's DI container after startup
- When detected: `WorkerHostService` starts the worker's Kestrel server on a pre-allocated ephemeral port; `CreateHttpClient()` returns a client backed by `AspNetCoreForwardingHandler` that rewrites request URIs and injects `x-ms-invocation-id`
- Startup filters (`InvocationIdStartupFilter`, `GrpcInvocationBridgeStartupFilter`) are registered in the worker's DI for all factory-backed hosts; they are no-ops when `ConfigureFunctionsWorkerDefaults()` is used (no `IApplicationBuilder` pipeline exists)

## 🔴 Current Blockers

_None. All known blockers have been resolved._

### ~~FunctionsWebApplicationFactory — POST/PUT Function ID Mismatch~~ ✅ FIXED

**What it was**: POST/PUT requests through `FunctionsWebApplicationFactory` failed because `GrpcHostService` used GUID-based function IDs while the worker's `_functionMap` used hash-based IDs from `GeneratedFunctionMetadataProvider`.

**Fix applied**: `GrpcHostService` now stores the hash-based `FunctionId` from `FunctionMetadataResponse` (the value computed by the worker's `GeneratedFunctionMetadataProvider`) directly in `_functionRouteToId`, so `SendInvocationRequestAsync` sends the correct ID that matches the worker's internal `_functionMap`.

### ~~FunctionsWebApplicationFactory — ObjectDisposedException after WithWebHostBuilder~~ ✅ FIXED

**What it was**: `CreateAndGetTodo_WorksEndToEnd` threw `ObjectDisposedException: IServiceProvider` and hung when run after `WithWebHostBuilder_CanOverrideServices` in the same test run.

**Root cause**: `GrpcWorker.StopAsync()` in the Azure Functions worker returns `Task.CompletedTask` immediately without closing the gRPC channel. As a result, when `customFactory.Dispose()` disposed the secondary host's DI container, the secondary worker was still connected to EventStream-2. The next test's `GrpcInvocationBridgeStartupFilter` wrote an `InvocationRequest` to `responseStream-2` (since EventStream-2 hadn't ended yet), which worker-2 received with its already-disposed `IServiceProvider`.

**Fix applied**: Added a `GrpcAwareHost` wrapper (in `FunctionsWebApplicationFactory.CreateHost`) that wraps derived factory hosts. On `Dispose()`, it cancels the EventStream's `CancellationTokenSource` and waits for `_eventStreamFinished` before calling `_inner.Dispose()`. This ensures `_responseStream` is restored to the primary worker's stream before the secondary host's DI is torn down.

### ~~FunctionsWebApplicationFactory — Host Startup Hang~~ ✅ FIXED

**Root Cause (confirmed)**: The hang was **not** in `base.CreateHost()`. Diagnostics showed `CreateHost` completed in ~0.5 s with the worker already connected. The actual hang was in **HTTP request handling**: `WorkerRequestServicesMiddleware` blocked on `SetHttpContextAsync()` waiting for the worker's `FunctionsHttpProxyingMiddleware` to call `SetFunctionContextAsync()` — which is only triggered by an `InvocationRequest` from the host.

**Fix applied**:
- Added `GrpcInvocationBridgeStartupFilter` to `FunctionsWebApplicationFactory.ConfigureWebHost()`: fires `SendInvocationRequestAsync` for every incoming request before `WorkerRequestServicesMiddleware` is reached.
- Added `GrpcHostService.FindFunctionId()` for route prefix stripping and `{param}` segment matching.
- Added `GrpcHostService.SendInvocationRequestAsync()` to send a minimal `InvocationRequest` to the worker.

### ~~POST/PUT Request Body Parsing (Critical)~~ ✅ FIXED

**Issue**: Functions that read the HTTP request body (POST/PUT) were failing with:
```
System.NotSupportedException: GrpcHttpRequestData expects binary data only.
The provided data type was 'String'.
```

**Fix applied** in `src/AzureFunctions.TestFramework.Core/Http/HttpRequestMapper.cs`:
- Changed `TypedData.String` to `TypedData.Bytes` (via `ByteString.CopyFromUtf8`) for both `Body` and `RawBody`.

## 🟡 Known Issues (Non-Blocking)

### 1. ~~Disposal Warnings~~ ✅ FIXED
**Symptoms previously**: During test cleanup:
```
Error in event stream
System.IO.IOException: The request stream was aborted.
ConnectionAbortedException: The connection was aborted because the server is shutting down
```

**Fix applied**: `FunctionsWebApplicationFactory` now centralizes cleanup for both `Dispose(bool)` and `DisposeAsync()`, requests EventStream shutdown explicitly, stops the gRPC server, and then disposes the logger/server resources. Derived `WithWebHostBuilder` hosts also support async disposal via `GrpcAwareHost.DisposeAsync()`.

### 2. ~~DI Service Overrides (FunctionsTestHost)~~ ✅ FIXED
**Status**: Dedicated `FunctionsTestHost` coverage now verifies both inline service replacement and factory-backed overrides in `Sample.FunctionApp.Worker.Tests\FunctionsTestHostFeaturesTests.cs`.

### 3. WAF suite runtime is still dominated by factory shutdown
**Status**: Non-blocking, measured but not yet resolved.

**What we measured**: Before this change, a targeted lifecycle diagnostic isolated the cost to the **primary `FunctionsWebApplicationFactory<Program>.Dispose()` call itself**, which took `30028ms`. In that run, factory construction was `119ms`, `CreateClient()` was `289ms`, requests were `11-74ms`, and derived factory disposal was only `8ms`. After implementing a framework-aware `DisposeAsync()` path and switching the WAF suite to an async-aware fixture wrapper, the latest validated WAF suite still took `38.1s` end-to-end even though the six test bodies reported only `251 ms` total work.

**Additional finding**: Calling the inherited `WebApplicationFactory.DisposeAsync()` path before this change completed in `17ms` (`factory ctor=114ms; CreateClient=272ms; request=75ms; DisposeAsync=17ms`), which proved that the inherited async path was bypassing framework-specific cleanup. The repository now has an explicit async cleanup path, but that proper cleanup path did not materially reduce the overall WAF suite wall-clock.

**What we tried**: Event-driven readiness is already in place. We also added explicit factory `DisposeAsync()`, async disposal support for derived hosts, an async-aware xUnit fixture wrapper, and reordered shutdown so the gRPC server is stopped before waiting for EventStream completion. These changes made cleanup semantics correct and consistent, but they did not produce a meaningful end-to-end runtime improvement.

**Current conclusion**: The remaining WAF slowness still comes from shutdown/lifecycle behavior rather than request handling. The framework now exposes the correct async disposal hooks, so future work should instrument the shutdown path beneath those hooks (`WebApplicationFactory`, isolated worker host shutdown, and gRPC server stop) rather than continuing to optimize request execution or test fixture wiring.

## 🔵 Future Enhancements

### 1. Additional Trigger Types
- ✅ Timer triggers (`AzureFunctions.TestFramework.Timer` package — `InvokeTimerAsync`)
- ✅ Queue triggers (`AzureFunctions.TestFramework.Queue` package — `InvokeQueueAsync`)
- ✅ Service Bus triggers (`AzureFunctions.TestFramework.ServiceBus` package — `InvokeServiceBusAsync`)
- Blob triggers  
- Event Grid triggers

### 2. Output Bindings
Currently focused on HttpTrigger input. Need to support:
- Queue output bindings
- Blob output bindings
- Table output bindings
- Return value bindings

### 3. Middleware Scenarios
- ✅ Custom middleware sample + end-to-end tests (`CorrelationIdMiddleware`)
- Authorization middleware
- Exception handling middleware

### 4. Configuration Support
- ✅ Override application settings via `FunctionsTestHostBuilder.ConfigureSetting()`
- Override host.json settings
- Environment variable helper APIs

### 5. ~~Parallel Test Execution~~ ✅ DONE
- Tests run in parallel between test collections (xUnit `parallelizeTestCollections: true`)
- gRPC tests: each test is isolated via `IAsyncLifetime` (per-test `FunctionsTestHost`)
- WAF tests: shared async-aware fixture wrapper around `FunctionsWebApplicationFactory<Program>` + per-test `InMemoryTodoService.Reset()` for state isolation; WAF tests still run in ~38 s
- Ephemeral gRPC ports ensure no port conflicts between parallel test instances

### 6. Performance Optimizations
- ✅ Event-driven startup/readiness replaced fixed startup delays and polling loops
- ✅ Direct gRPC route matching now precompiles templates once per host instead of rescanning raw route strings per request
- ✅ `FunctionsTestHost.CreateHttpClient()` now reuses host-local handlers instead of rebuilding them for every client
- ✅ Optional shared-host gRPC fixture example added for suites that can reset state between tests
- Default full-suite worker reuse remains opt-in because the current per-test-host model gives the safest isolation semantics

## Testing Commands

```bash
# Build solution
dotnet build --configuration Release

# Worker SDK 2.x gRPC tests (.NET 9)
dotnet test tests/Sample.FunctionApp.Worker.Tests --no-build --configuration Release

# Worker SDK 2.x WAF tests (.NET 9)
dotnet test tests/Sample.FunctionApp.Worker.WAF.Tests --no-build --configuration Release

# Run single test with detailed output
dotnet test tests/Sample.FunctionApp.Worker.Tests --filter "GetTodos_ReturnsEmptyList" --logger "console;verbosity=detailed"

# Pack NuGet packages locally (requires git tag for a clean version, otherwise MinVer uses 0.0.0-alpha.x)
dotnet pack --configuration Release --output ./artifacts
```

## Useful References

### Azure Functions Worker SDK
- Repo: https://github.com/Azure/azure-functions-dotnet-worker
- Key files:
  - `src/DotNetWorker/Hosting/WorkerHostBuilderExtensions.cs` - Worker configuration
  - `extensions/Worker.Extensions.Rpc/src/ConfigurationExtensions.cs` - RPC config
  - `sdk/Sdk/Targets/Microsoft.Azure.Functions.Worker.Sdk.targets` - Build targets

### Protocol Definitions
- Repo: https://github.com/Azure/azure-functions-language-worker-protobuf
- File: `src/proto/FunctionRpc.proto` - gRPC service definitions

### ASP.NET Core TestServer (Reference)
- Package: Microsoft.AspNetCore.Mvc.Testing
- Class: WebApplicationFactory<T>
- Our goal: Provide similar experience for Azure Functions

## Version Information
- .NET: 8.0 / 9.0 / 10.0 (multitargeted)
- Azure Functions Worker SDK 2.x: 2.51.0 (net9.0 sample)
- Grpc.AspNetCore: 2.62.0
- xUnit: 2.4.2

Last Updated: 2026-03-16
