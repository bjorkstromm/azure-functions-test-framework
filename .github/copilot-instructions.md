# Azure Functions Test Framework - Copilot Instructions

## Session Rules

> **Always update `README.md` and `KNOWN_ISSUES.md` at the end of every session** to reflect the current state of the project: what now works, what is still blocked, and what changed. These are the primary documentation files used to track progress between sessions.

## Project Overview
This is an integration testing framework for Azure Functions (dotnet-isolated) that provides a TestServer/WebApplicationFactory-like experience. It runs Azure Functions in-process without func.exe, communicating via the worker's gRPC endpoints.

**Current Status**: Both testing approaches are functional. The gRPC-based `FunctionsTestHost` supports full CRUD. `FunctionsWebApplicationFactory` works for GET requests; POST/PUT has a function ID mismatch issue (see KNOWN_ISSUES.md).

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

2. **AzureFunctions.TestFramework.AspNetCore**: WebApplicationFactory-based testing
   - `FunctionsWebApplicationFactory<TProgram>`: Extends `WebApplicationFactory<TProgram>`
   - `InvocationIdStartupFilter`: Injects `x-ms-invocation-id` header when absent
   - `GrpcInvocationBridgeStartupFilter`: Fires `InvocationRequest` for every HTTP request, unblocking `WorkerRequestServicesMiddleware`

3. **AzureFunctions.TestFramework.Http**: HTTP-specific functionality (placeholder)

4. **Sample.FunctionApp**: Example functions for testing (TodoAPI with CRUD operations)

5. **Sample.FunctionApp.Tests**: gRPC-based integration tests (`FunctionsTestHost`)

6. **Sample.FunctionApp.WebApplicationFactory.Tests**: WebApplicationFactory-based integration tests

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

## Critical Technical Details

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

### Function ID Mismatch Issue (FunctionsWebApplicationFactory)
`GeneratedFunctionMetadataProvider` computes a stable hash for each function (`Name` + `ScriptFile` + `EntryPoint`). Our `GrpcHostService` stores the GUID-based `FunctionId` from `FunctionLoadRequest` in `_functionRouteToId`. When `FunctionsEndpointDataSource` calls `GetFunctionMetadataAsync` directly, it gets hash-based IDs — causing a mismatch in `FunctionsApplication._functionMap` at invocation time.

## Development Guidelines

### Testing Approach
```bash
# Build solution
dotnet build

# Run gRPC-based tests
dotnet test tests/Sample.FunctionApp.Tests

# Run WebApplicationFactory tests
dotnet test tests/Sample.FunctionApp.WebApplicationFactory.Tests

# Run single test
dotnet test tests/Sample.FunctionApp.Tests --filter "GetTodos_ReturnsEmptyList" --logger "console;verbosity=detailed"
```

### Code Style
- Use nullable reference types
- Add XML documentation for public APIs
- Follow existing patterns in GrpcHostService for async message handling
- Don't block the gRPC event stream (use Task.Run for long-running operations)

### Testing
- Sample.FunctionApp has TodoAPI with 7 HTTP endpoints (CRUD + Health + Echo)
- 8 integration tests in `Sample.FunctionApp.Tests` (gRPC-based)
- 4 integration tests in `Sample.FunctionApp.WebApplicationFactory.Tests`
- Tests use IAsyncLifetime pattern for setup/cleanup (gRPC tests) or IClassFixture (WAF tests)

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
    
  AzureFunctions.TestFramework.AspNetCore/
    WebApplicationFactory-based testing
    └── FunctionsWebApplicationFactory.cs
    
  AzureFunctions.TestFramework.Http/
    HTTP-specific functionality (placeholder)
    
samples/
  Sample.FunctionApp/
    Example Azure Functions app with TodoAPI
    
tests/
  Sample.FunctionApp.Tests/
    gRPC-based integration tests (FunctionsTestHost)
  Sample.FunctionApp.WebApplicationFactory.Tests/
    WebApplicationFactory-based integration tests
```

## References
- Azure Functions Worker: https://github.com/Azure/azure-functions-dotnet-worker
- RPC Protocol: https://github.com/Azure/azure-functions-language-worker-protobuf
- Worker Configuration: See WorkerHostBuilderExtensions.cs in azure-functions-dotnet-worker

## Success Metrics
✅ Solution builds successfully
✅ Worker starts in-process using HostBuilder
✅ Worker connects to gRPC server
✅ gRPC bidirectional streaming works
✅ Function loading/discovery (all 7 functions)
✅ Function invocation works (FunctionsTestHost — all HTTP methods)
✅ All FunctionsTestHost integration tests pass
✅ FunctionsWebApplicationFactory GET requests work
⚠️ FunctionsWebApplicationFactory POST/PUT — function ID mismatch (see KNOWN_ISSUES.md)
❌ DI service overrides not tested

