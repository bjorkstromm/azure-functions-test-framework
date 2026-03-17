# Azure Functions Test Framework - Copilot Instructions

## Session Rules

> **Always update `README.md` and `KNOWN_ISSUES.md` at the end of every session** to reflect the current state of the project: what now works, what is still blocked, and what changed. These are the primary documentation files used to track progress between sessions.

## Project Overview
This is an integration testing framework for Azure Functions (dotnet-isolated) that provides a TestServer/WebApplicationFactory-like experience. It runs Azure Functions in-process without func.exe, communicating via the worker's gRPC endpoints.

**Current Status**: Both testing approaches are **fully functional** for the current **Worker SDK 2.x (.NET 9)** sample. The gRPC-based `FunctionsTestHost` supports full CRUD, TimerTrigger, QueueTrigger, ServiceBusTrigger, middleware assertions, `Services`, and `ConfigureSetting()`. `FunctionsWebApplicationFactory` supports full CRUD including POST/PUT/DELETE, middleware assertions, and `WithWebHostBuilder` service overrides. Startup/readiness is event-driven and the direct gRPC path precompiles route matching per host. All framework libraries target `net8.0;net9.0;net10.0`. Tests run in parallel and in isolation. No known blockers.

## Architecture

### Key Components
1. **AzureFunctions.TestFramework.Core**: Main framework
   - `FunctionsTestHost`: Orchestrates worker startup and gRPC communication
   - `GrpcHostService`: Implements Azure Functions host gRPC protocol (bidirectional streaming)
     - `FindFunctionId(method, path, routePrefix)`: Route matching with `{param}` support
     - `SendInvocationRequestAsync(invocationId, method, path)`: Fires InvocationRequest to worker
   - `GrpcServerManager`: Manages Kestrel-based gRPC server lifecycle
   - `WorkerHostService`: Starts Azure Functions Worker using HostBuilder (in-process)
   - `FunctionsHttpMessageHandler`: Custom HttpMessageHandler for intercepting HTTP requests
   - `HttpRequestMapper`/`HttpResponseMapper`: Convert between HTTP and gRPC messages

2. **AzureFunctions.TestFramework.Http.AspNetCore**: WebApplicationFactory-based testing
   - `FunctionsWebApplicationFactory<TProgram>`: Extends `WebApplicationFactory<TProgram>`
   - `InvocationIdStartupFilter`: Injects `x-ms-invocation-id` header when absent
   - `GrpcInvocationBridgeStartupFilter`: Fires `InvocationRequest` for every HTTP request, unblocking `WorkerRequestServicesMiddleware`
   - `GrpcAwareHost`: Wraps derived factory hosts (created by `WithWebHostBuilder`); cancels the secondary EventStream and waits for it to finish before calling `_inner.Dispose()` — prevents `ObjectDisposedException` race

3. **AzureFunctions.TestFramework.Http**: HTTP-specific functionality (placeholder)

4. **AzureFunctions.TestFramework.Timer**: TimerTrigger invocation support — depends on Core + `Microsoft.Azure.Functions.Worker.Extensions.Timer`. Exposes `InvokeTimerAsync(this IFunctionsTestHost, string functionName, TimerInfo? timerInfo = null)` extension method.

5. **Sample.FunctionApp.Worker**: Example functions for testing (TodoAPI with CRUD operations + HeartbeatTimerFunction + Correlation middleware sample)

6. **Sample.FunctionApp.Worker.Tests**: `FunctionsTestHost` integration tests

7. **Sample.FunctionApp.Worker.WAF.Tests**: WebApplicationFactory-based integration tests

### How It Works

#### FunctionsTestHost (gRPC path)
1. **Build Phase**: `FunctionsTestHostBuilder.Build()` creates GrpcServerManager and starts it to get an ephemeral port
2. **Startup Phase**: 
   - gRPC server is already listening (started in Build)
   - WorkerHostService creates HostBuilder, configures it to connect to gRPC server
   - Worker's `StartAsync()` connects to our gRPC server
   - GrpcHostService handles bidirectional streaming (EventStream RPC)
3. **Testing Phase**:
   - Test creates HttpClient via `testHost.CreateHttpClient()`
   - HttpClient uses FunctionsHttpMessageHandler
   - Handler converts HTTP request → gRPC InvocationRequest
   - Sends to worker via GrpcHostService
   - Worker executes function, returns response
   - Handler converts gRPC InvocationResponse → HTTP response

#### FunctionsWebApplicationFactory (ASP.NET Core path)
1. **Constructor**: Starts gRPC server eagerly (before host is built) so port is known
2. **CreateHost**: Injects gRPC config into host builder; registers `IAutoConfigureStartup` types from the functions assembly; calls `base.CreateHost(builder)` which starts `TestServer`
3. **ConfigureWebHost**:
   - `InvocationIdStartupFilter`: first middleware — injects synthetic `x-ms-invocation-id`
   - `GrpcInvocationBridgeStartupFilter`: second middleware — fires `SendInvocationRequestAsync` so `WorkerRequestServicesMiddleware` unblocks
4. **Testing Phase**: `factory.CreateClient()` returns an `HttpClient` pointed at the `TestServer`

#### Timer Trigger Invocation
1. **Function discovery**: `GrpcHostService` parses `timerTrigger` bindings during `HandleFunctionsMetadataResponse`, populating `_timerFunctionMap[functionName] = (FunctionId, ParameterName)`
2. **API**: `host.InvokeTimerAsync("HeartbeatTimer", timerInfo?)` (from `AzureFunctions.TestFramework.Timer`)
3. **Flow**: Extension method serializes `TimerInfo` → camelCase JSON, puts it at `context.InputData["$timerJson"]`, calls `host.Invoker.InvokeAsync` with `TriggerType = "timerTrigger"`; Core reads it back and calls `GrpcHostService.InvokeTimerFunctionAsync` which builds an `InvocationRequest` with the timer JSON as `ParameterBinding`



### Worker Configuration
The worker needs these configuration keys (set in WorkerHostService / FunctionsWebApplicationFactory):
- `Functions:Worker:HostEndpoint` - gRPC server URI (e.g., "http://127.0.0.1:PORT")
- `Functions:Worker:WorkerId` - Unique GUID
- `Functions:Worker:RequestId` - Unique GUID  
- `Functions:Worker:GrpcMaxMessageLength` - "2147483647"

### gRPC Protocol
Uses Azure Functions RPC protocol from `azure-functions-language-worker-protobuf`:
- **FunctionRpc.EventStream**: Bidirectional streaming RPC
- **Key messages**: StartStream, WorkerInitRequest/Response, FunctionsMetadataRequest/Response, FunctionLoadRequest/Response, InvocationRequest/Response

### IAutoConfigureStartup (Critical)
The functions assembly contains source-generated classes (`FunctionMetadataProviderAutoStartup`, `FunctionExecutorAutoStartup`) implementing `IAutoConfigureStartup`. Both the `WorkerHostService` and `FunctionsWebApplicationFactory.CreateHost` scan for and invoke these to register `GeneratedFunctionMetadataProvider` and `DirectFunctionExecutor`, overriding the defaults that would require a `functions.metadata` file.

### Function ID Resolution
`GeneratedFunctionMetadataProvider` computes a stable hash for each function (`Name` + `ScriptFile` + `EntryPoint`). `GrpcHostService` stores the hash-based `FunctionId` from `FunctionMetadataResponse` in `_functionRouteToId` (not the GUID from `FunctionLoadRequest`), so `SendInvocationRequestAsync` sends the correct ID that matches the worker's internal `_functionMap`.

### GrpcWorker.StopAsync() is a No-Op
The Azure Functions worker SDK's `GrpcWorker.StopAsync()` returns `Task.CompletedTask` immediately — it does NOT close the gRPC channel. This affects both WAF disposal and the `WithWebHostBuilder` scenario:
- **WAF disposal**: `FunctionsWebApplicationFactory.Dispose` calls `_grpcHostService.SignalShutdownAsync()` after `base.Dispose()` but before `_grpcServerManager.StopAsync()`. This gracefully ends the EventStream so Kestrel can stop instantly (no 5 s `ShutdownTimeout` wait).
- **WithWebHostBuilder**: `GrpcAwareHost` wraps the derived factory's host and cancels that EventStream + waits for it to finish before `_inner.Dispose()`, preventing `ObjectDisposedException`.

## Development Guidelines

### Testing Approach
```bash
# Build solution
dotnet build

# Run gRPC-based tests
dotnet test tests/Sample.FunctionApp.Worker.Tests

# Run WebApplicationFactory tests
dotnet test tests/Sample.FunctionApp.Worker.WAF.Tests

# Run single test
dotnet test tests/Sample.FunctionApp.Worker.Tests --filter "GetTodos_ReturnsEmptyList" --logger "console;verbosity=detailed"
```

### Code Style
- Use nullable reference types
- Add XML documentation for public APIs
- Follow existing patterns in GrpcHostService for async message handling
- Don't block the gRPC event stream (use Task.Run for long-running operations)

### Testing
- gRPC tests use `IAsyncLifetime` per-test (each test gets its own `FunctionsTestHost`)
- WAF tests use `IClassFixture<FunctionsWebApplicationFactory<Program>>` (one shared factory) + `IAsyncLifetime` to call `InMemoryTodoService.Reset()` for per-test state isolation

## Project Structure
```
src/
  AzureFunctions.TestFramework.Core/
    Core abstractions, gRPC server, worker hosting
    ├── Grpc/
    │   ├── GrpcHostService.cs         # Bidirectional streaming handler + route matching
    │   ├── GrpcServerManager.cs       # Kestrel server lifecycle
    │   └── GrpcLoggingInterceptor.cs  # Logging middleware
    ├── Worker/
    │   └── WorkerHostService.cs       # In-process worker hosting
    ├── Http/
    │   ├── HttpRequestMapper.cs       # HTTP → gRPC conversion
    │   └── HttpResponseMapper.cs      # gRPC → HTTP conversion
    ├── Client/
    │   └── FunctionsHttpMessageHandler.cs  # Custom HttpMessageHandler
    ├── Protos/
    │   └── FunctionRpc.proto          # Azure Functions RPC protocol
    ├── FunctionsTestHost.cs           # Main orchestrator
    └── FunctionsTestHostBuilder.cs    # Fluent builder API
    
  AzureFunctions.TestFramework.Http.AspNetCore/
    WebApplicationFactory-based testing
    └── FunctionsWebApplicationFactory.cs
    
  AzureFunctions.TestFramework.Http/
    HTTP-specific functionality (placeholder)
    
samples/
  Sample.FunctionApp.Worker/
    Worker SDK 2.x sample (net9.0) — TodoAPI + Correlation middleware + HeartbeatTimer + ServiceBus + Queue
    
tests/
  Sample.FunctionApp.Worker.Tests/
    gRPC-based integration tests for Worker SDK 2.x (net9.0)
  Sample.FunctionApp.Worker.WAF.Tests/
    WAF-based integration tests for Worker SDK 2.x (net9.0)
```

## References
- Azure Functions Worker: https://github.com/Azure/azure-functions-dotnet-worker
- RPC Protocol: https://github.com/Azure/azure-functions-language-worker-protobuf
- Worker Configuration: See WorkerHostBuilderExtensions.cs in azure-functions-dotnet-worker

## Success Metrics
✅ Solution builds successfully (net8.0 / net9.0 / net10.0)
✅ Worker starts in-process using HostBuilder
✅ Worker connects to gRPC server
✅ gRPC bidirectional streaming works
✅ Function loading/discovery
✅ Function invocation works (FunctionsTestHost — all HTTP methods + TimerTrigger)
✅ All FunctionsTestHost integration tests pass
✅ FunctionsWebApplicationFactory works for all HTTP methods (GET, POST, PUT, DELETE)
✅ WithWebHostBuilder DI service overrides work end-to-end
✅ Tests run in parallel and in isolation (xUnit parallelizeTestCollections + IAsyncLifetime)
✅ Graceful gRPC EventStream shutdown (no connection-abort errors, no Kestrel 5 s timeout)
✅ CI workflow runs on pull requests and pushes to main
✅ Current sample targets Worker SDK 2.x (2.51.0)
✅ All framework libraries target net8.0;net9.0;net10.0

