# Known Issues and Current Status

## 🟢 What Works

### Core Infrastructure ✅
- Solution structure with 4 projects builds successfully
- All NuGet dependencies resolve correctly
- gRPC protocol definitions integrated from azure-functions-language-worker-protobuf

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
- FunctionsMetadataRequest/Response returns all 7 functions
- FunctionLoadRequest/Response succeeds for all functions
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
- Sample TodoAPI function app with 7 HTTP endpoints (including Health + Echo)
- 14 integration tests in `Sample.FunctionApp.Tests` (gRPC-based, `FunctionsTestHost`): 1 unit + 7 TodoFunctions + 3 DI override tests
- 3 timer integration tests in `Sample.FunctionApp.Tests` (via `AzureFunctions.TestFramework.Timer`)
- 4 function metadata discovery tests in `Sample.FunctionApp.Tests` (via `IFunctionInvoker.GetFunctions()`)
- 4 integration tests in `Sample.FunctionApp.WebApplicationFactory.Tests` (`FunctionsWebApplicationFactory`)
- `IAsyncLifetime` pattern for per-test setup/cleanup (each gRPC test gets its own isolated host; WAF tests share one factory via `IClassFixture` with per-test `InMemoryTodoService.Reset()` for state isolation)
- Tests run in parallel between test collections (`xunit.runner.json` with `parallelizeTestCollections: true`)
- xUnit integration working
- All `FunctionsTestHost` tests pass (GET, POST, PUT, DELETE, 404, function metadata discovery)
- All `FunctionsWebApplicationFactory` tests pass (GET, POST, PUT, DELETE, `WithWebHostBuilder` service overrides)
- Graceful gRPC EventStream shutdown on test teardown (no connection-abort errors, no Kestrel 5 s timeout)

### FunctionsWebApplicationFactory ✅
- `GrpcInvocationBridgeStartupFilter` fires an `InvocationRequest` for every incoming HTTP request, unblocking `WorkerRequestServicesMiddleware`
- `InvocationIdStartupFilter` injects `x-ms-invocation-id` header when absent
- `GrpcHostService.FindFunctionId()` matches routes with `{param}` placeholder support
- Host startup completes in ~0.5 s — no longer hangs
- All HTTP methods (GET, POST, PUT, DELETE) pass end-to-end
- `WithWebHostBuilder` service overrides work — secondary worker EventStream ends cleanly before host DI is disposed

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

**Fix applied**: `FunctionsWebApplicationFactory.Dispose` now calls `_grpcHostService.SignalShutdownAsync()` after `base.Dispose()` but before `_grpcServerManager.StopAsync()`. This ends the EventStream gracefully so Kestrel can stop instantly without waiting for the 5-second `HostOptions.ShutdownTimeout`.

### 2. ~~DI Service Overrides (FunctionsTestHost)~~ ✅ Partially Tested
**Status**: DI service overrides work in `FunctionsWebApplicationFactory` (tested via `WithWebHostBuilder_CanOverrideServices`). For `FunctionsTestHost`, the infrastructure is in place (`FunctionsTestHostBuilder.ConfigureServices()` / `WorkerHostService.ConfigureServices()`) but no dedicated override tests exist yet.

## 🔵 Future Enhancements

### 1. Additional Trigger Types
- ✅ Timer triggers (`AzureFunctions.TestFramework.Timer` package — `InvokeTimerAsync`)
- Queue triggers
- Blob triggers  
- Event Grid triggers
- Service Bus triggers

### 2. Output Bindings
Currently focused on HttpTrigger input. Need to support:
- Queue output bindings
- Blob output bindings
- Table output bindings
- Return value bindings

### 3. Middleware Testing
Support for testing:
- Custom middleware
- Authorization middleware
- Exception handling middleware

### 4. Configuration Support
- Override host.json settings
- Override application settings
- Environment variable support

### 5. ~~Parallel Test Execution~~ ✅ DONE
- Tests run in parallel between test collections (xUnit `parallelizeTestCollections: true`)
- gRPC tests: each test is isolated via `IAsyncLifetime` (per-test `FunctionsTestHost`)
- WAF tests: shared `IClassFixture<FunctionsWebApplicationFactory<Program>>` factory + per-test `InMemoryTodoService.Reset()` for state isolation; WAF tests run in ~37 s
- Ephemeral gRPC ports ensure no port conflicts between parallel test instances

### 6. Performance Optimizations
- Reuse worker instances across tests
- Lazy initialization
- Connection pooling

## Testing Commands

```bash
# Build solution
dotnet build

# Run gRPC-based tests
dotnet test tests/Sample.FunctionApp.Tests

# Run WebApplicationFactory-based tests
dotnet test tests/Sample.FunctionApp.WebApplicationFactory.Tests

# Run single test with detailed output
dotnet test tests/Sample.FunctionApp.Tests --filter "GetTodos_ReturnsEmptyList" --logger "console;verbosity=detailed"
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
- .NET: 8.0
- Azure Functions Worker: 1.21.0
- Grpc.AspNetCore: 2.62.0
- xUnit: 2.4.2

Last Updated: 2026-03-03 (session 8)
