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
- 8 comprehensive integration tests in `Sample.FunctionApp.Tests` (gRPC-based, `FunctionsTestHost`)
- 4 integration tests in `Sample.FunctionApp.WebApplicationFactory.Tests` (`FunctionsWebApplicationFactory`)
- IAsyncLifetime pattern for test setup/cleanup
- xUnit integration working
- All `FunctionsTestHost` tests pass (GET, POST, PUT, DELETE, 404)
- `FunctionsWebApplicationFactory` GET tests pass

### FunctionsWebApplicationFactory ✅ (partially)
- `GrpcInvocationBridgeStartupFilter` fires an `InvocationRequest` for every incoming HTTP request, unblocking `WorkerRequestServicesMiddleware`
- `InvocationIdStartupFilter` injects `x-ms-invocation-id` header when absent
- `GrpcHostService.FindFunctionId()` matches routes with `{param}` placeholder support
- Host startup completes in ~0.5 s — no longer hangs
- GET requests (`Health`, `GetTodos`) pass end-to-end

## 🔴 Current Blockers

### FunctionsWebApplicationFactory — POST/PUT Function ID Mismatch

**What it is**: When a POST or PUT request is sent through `FunctionsWebApplicationFactory`, the worker's `FunctionsApplication.CreateContext()` throws `KeyNotFoundException` for the function's computed hash ID (e.g. `3897823149`).

**Root cause**: `GeneratedFunctionMetadataProvider` (source-generated) computes a stable hash for each `DefaultFunctionMetadata` (`Name` + `ScriptFile` + `EntryPoint`). Our `GrpcHostService` assigns a new `FunctionId` (GUID) per `FunctionLoadRequest`. When `FunctionsEndpointDataSource.BuildEndpoints()` calls `GetFunctionMetadataAsync()` **directly** on the `GeneratedFunctionMetadataProvider`, the metadata objects get their hash-based IDs — different from the GUID IDs our host assigned via `FunctionLoadRequest`. The `_functionMap` is therefore keyed by GUIDs, but the invocation arrives with the hash-based ID, causing the lookup to fail.

**Impact**: `CreateAndGetTodo_WorksEndToEnd` (POST) and any PUT/DELETE tests via `FunctionsWebApplicationFactory` hang waiting for `SetFunctionContextAsync`.

**Next Steps**:
1. In `GrpcHostService.SendInvocationRequestAsync`, use the function ID from `FunctionMetadataResponse` (the hash-based one returned by the worker's `GeneratedFunctionMetadataProvider`) rather than the GUID assigned in `FunctionLoadRequest`.
2. Alternatively, update `GrpcHostService` to store the hash-based `FunctionId` from `FunctionMetadataResponse` in `_functionRouteToId` so `SendInvocationRequestAsync` sends the correct ID.

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

### 1. Disposal Warnings
**Symptoms**: During test cleanup, see warnings:
```
Error in event stream
System.IO.IOException: The request stream was aborted.
ConnectionAbortedException: The connection was aborted because the server is shutting down
```

**Impact**: Low - tests complete successfully, just noisy logs

**Solution**: Implement graceful shutdown:
- Stop the worker host before stopping the gRPC server
- Increase `HostOptions.ShutdownTimeout`

### 2. DI Service Overrides Not Tested
**Status**: Infrastructure in place but not tested

**Location**: 
- `WorkerHostService.ConfigureServices()`
- `FunctionsTestHostBuilder.ConfigureServices()`

**Next Steps**: Add tests for:
- Replacing services in DI container
- Verifying overridden services are used
- Scoped vs singleton behavior

## 🔵 Future Enhancements

### 1. Additional Trigger Types
- Timer triggers
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

### 5. Parallel Test Execution
- Ensure thread-safety
- Port conflict handling
- State isolation between tests

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

# Run WebApplicationFactory GET tests (currently passing)
dotnet test tests/Sample.FunctionApp.WebApplicationFactory.Tests --filter "GetTodos_ReturnsSuccessStatusCode|Health_ReturnsHealthyStatus"
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

Last Updated: 2026-03-03 (session 2)
