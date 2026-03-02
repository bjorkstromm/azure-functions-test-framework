# Azure Functions Test Framework - Copilot Instructions

## Project Overview
This is an integration testing framework for Azure Functions (dotnet-isolated) that provides a TestServer/WebApplicationFactory-like experience. It runs Azure Functions in-process without func.exe, communicating via the worker's gRPC endpoints.

**Current Status**: Core infrastructure complete, worker connects successfully to gRPC server. Function loading/invocation needs work.

## Architecture

### Key Components
1. **AzureFunctions.TestFramework.Core**: Main framework
   - `FunctionsTestHost`: Orchestrates worker startup and gRPC communication
   - `GrpcHostService`: Implements Azure Functions host gRPC protocol (bidirectional streaming)
   - `GrpcServerManager`: Manages Kestrel-based gRPC server lifecycle
   - `WorkerHostService`: Starts Azure Functions Worker using HostBuilder (in-process)
   - `FunctionsHttpMessageHandler`: Custom HttpMessageHandler for intercepting HTTP requests
   - `HttpRequestMapper`/`HttpResponseMapper`: Convert between HTTP and gRPC messages

2. **AzureFunctions.TestFramework.Http**: HTTP-specific functionality (placeholder)

3. **Sample.FunctionApp**: Example functions for testing (TodoAPI with CRUD operations)

4. **Sample.FunctionApp.Tests**: Integration tests demonstrating framework usage

### How It Works
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

## Critical Technical Details

### Worker Configuration
The worker needs these configuration keys (set in WorkerHostService):
- `Functions:Worker:HostEndpoint` - gRPC server URI (e.g., "http://127.0.0.1:PORT")
- `Functions:Worker:WorkerId` - Unique GUID
- `Functions:Worker:RequestId` - Unique GUID  
- `Functions:Worker:GrpcMaxMessageLength` - "2147483647"

### gRPC Protocol
Uses Azure Functions RPC protocol from `azure-functions-language-worker-protobuf`:
- **FunctionRpc.EventStream**: Bidirectional streaming RPC
- **Key messages**: StartStream, WorkerInitRequest/Response, FunctionsMetadataRequest/Response, FunctionLoadRequest/Response, InvocationRequest/Response

### Port Discovery Issue (SOLVED)
The gRPC server finds an ephemeral port, but WorkerHostService needs to know this port. **Solution**: Start GrpcServerManager during `Build()` (not `StartAsync()`), then pass the actual port to WorkerHostService.

## Known Issues & Current Blockers

### 🔴 BLOCKER: Function Loading/Discovery
**Status**: Worker connects successfully but functions aren't being loaded/invoked properly.

**Symptoms**:
- Worker connects to gRPC server ✅
- WorkerInitRequest sent ✅
- FunctionsMetadataRequest returns 0 functions ❌
- HTTP requests return 500 Internal Server Error
- Error message: "Error invoking function: A task was canceled"

**Root Cause**: 
The .NET isolated worker needs function metadata to discover functions. This metadata is typically:
1. Generated at build time by the Azure Functions Worker SDK
2. Stored in `.functions.json` files or embedded resources
3. Read by the worker during initialization

**Current Problem**:
- `FunctionAppDirectory` is set to the assembly's location
- Worker can't find function metadata files
- No `.functions.json` files in Sample.FunctionApp output
- Metadata generation might require special build configuration

**Next Steps**:
1. Research how dotnet-isolated worker discovers functions (metadata files? reflection? code generation?)
2. Check if Sample.FunctionApp needs special build properties for metadata generation
3. Examine Azure Functions Worker SDK source to understand function loading
4. Consider alternative: Use FunctionMetadataProvider directly instead of gRPC metadata request
5. Look at `Microsoft.Azure.Functions.Worker.Sdk` targets for metadata generation

## Development Guidelines

### When Working on Function Loading
- **Critical files**:
  - `src/AzureFunctions.TestFramework.Core/Grpc/GrpcHostService.cs` (HandleStartStreamAsync - function loading logic)
  - `src/AzureFunctions.TestFramework.Core/Worker/WorkerHostService.cs` (worker configuration)
  - `samples/Sample.FunctionApp/Sample.FunctionApp.csproj` (build configuration)
  
- **Key areas to investigate**:
  - Azure Functions Worker SDK source code (github.com/Azure/azure-functions-dotnet-worker)
  - Metadata generation targets in Worker.Sdk
  - FunctionMetadataProvider and IFunctionMetadataProvider
  - DefaultFunctionMetadataProvider implementation
  
- **Testing approach**:
  ```bash
  # Build and run single test
  dotnet build
  dotnet test tests/Sample.FunctionApp.Tests --filter "GetTodos_ReturnsEmptyList"
  
  # Check for function metadata files
  Get-ChildItem -Path samples/Sample.FunctionApp/bin/Debug/net8.0 -Recurse -Filter "*.json"
  ```

### Code Style
- Use nullable reference types
- Add XML documentation for public APIs
- Follow existing patterns in GrpcHostService for async message handling
- Don't block the gRPC event stream (use Task.Run for long-running operations)

### Testing
- Sample.FunctionApp has TodoAPI with 6 endpoints (CRUD + Health + Echo)
- 8 integration tests in TodoFunctionsTests.cs
- Tests use IAsyncLifetime pattern for setup/cleanup

## Project Structure
```
src/
  AzureFunctions.TestFramework.Core/
    Core abstractions, gRPC server, worker hosting
    ├── Grpc/
    │   ├── GrpcHostService.cs         # Bidirectional streaming handler
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
    
  AzureFunctions.TestFramework.Http/
    HTTP-specific functionality (placeholder)
    
samples/
  Sample.FunctionApp/
    Example Azure Functions app with TodoAPI
    
tests/
  Sample.FunctionApp.Tests/
    Integration tests demonstrating framework
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
⚠️ Function loading/discovery (BLOCKED)
❌ Function invocation works
❌ HTTP integration tests pass
❌ DI service overrides work
