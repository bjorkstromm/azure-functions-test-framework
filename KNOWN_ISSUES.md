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
- 8 comprehensive integration tests written
- IAsyncLifetime pattern for test setup/cleanup
- xUnit integration working
- GET-only tests pass (GetTodos, GetTodo_NotFound, Health)

## 🔴 Current Blockers

### FunctionsWebApplicationFactory — Host Startup Hang (Investigation Required)

**What it is**: `FunctionsWebApplicationFactory<TProgram>` (in `AzureFunctions.TestFramework.AspNetCore`) extends `WebApplicationFactory<TProgram>` and wires up the in-process gRPC host handshake. The goal is to run the full ASP.NET Core pipeline from `Program.cs` — including custom middleware and services — through `TestServer`.

**Issue**: The factory currently hangs during `base.CreateHost(builder)`. The gRPC handshake itself works fine in isolation (confirmed via standalone diagnostic).

**Root Cause Investigation**:
- `ConfigureFunctionsWebApplication()` registers `FunctionsEndpointDataSource`, which during `host.Start()` tries to read `functions.metadata` or contact the gRPC host.
- With `TestServer`, the `WebApplicationFactory` may call `host.Start()` synchronously in a context where the gRPC event loop cannot complete the handshake.
- The `IAutoConfigureStartup` fix (registers `GeneratedFunctionMetadataProvider`) and the `InvocationIdStartupFilter` (auto-injects `x-ms-invocation-id`) are already in place and correct.

**Next Steps**:
1. Investigate whether `FunctionsEndpointDataSource.BuildEndpoints()` blocks waiting for gRPC metadata — if so, the gRPC server must be connected *before* `host.Start()` is called.
2. Try creating the host but delaying `host.Start()` until after the gRPC worker has connected.
3. Explore whether `CreateHostBuilder` → `Build()` then manually `StartAsync()` gives more control than `base.CreateHost()`.

### POST/PUT Request Body Parsing (Critical)

**Issue**: Functions that read the HTTP request body (POST/PUT) fail with:
```
System.NotSupportedException: GrpcHttpRequestData expects binary data only.
The provided data type was 'String'.
```

**Affected tests**: `CreateTodo`, `GetTodo_WhenExists`, `UpdateTodo`, `DeleteTodo`, `DeleteTodo` (all involve a POST/PUT first)

**Root Cause**: `HttpRequestMapper` sets the request body as `TypedData.String`, but `GrpcHttpRequestData` in the .NET isolated worker only accepts `TypedData.Bytes`.

**Fix required** in `src/AzureFunctions.TestFramework.Core/Http/HttpRequestMapper.cs`:
```csharp
// Change:
httpRequest.Body = new TypedData { String = body };
// To:
httpRequest.Body = new TypedData { Bytes = Google.Protobuf.ByteString.CopyFromUtf8(body) };
```

**Also set `rawBody`** for completeness:
```csharp
httpRequest.RawBody = new TypedData { Bytes = Google.Protobuf.ByteString.CopyFromUtf8(body) };
```

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

# Run all tests
dotnet test tests/Sample.FunctionApp.Tests

# Run single test with detailed output
dotnet test tests/Sample.FunctionApp.Tests --filter "GetTodos_ReturnsEmptyList" --logger "console;verbosity=detailed"

# Run only currently passing tests
dotnet test tests/Sample.FunctionApp.Tests --filter "GetTodos_ReturnsEmptyList|GetTodo_ReturnsNotFound|Health_ReturnsOk"
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

Last Updated: 2026-03-03
