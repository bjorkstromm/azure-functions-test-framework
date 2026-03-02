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

### gRPC Communication ✅
- GrpcServerManager starts Kestrel with HTTP/2 on ephemeral port
- GrpcHostService implements FunctionRpc bidirectional streaming
- Worker connects and sends StartStream message
- WorkerInitRequest/Response exchange successful
- Logging and interceptor infrastructure working

### HTTP Client API ✅
- FunctionsTestHostBuilder with fluent API
- CreateHttpClient() returns HttpClient with custom handler
- FunctionsHttpMessageHandler intercepts HTTP requests
- HttpRequestMapper converts HTTP → gRPC InvocationRequest
- HttpResponseMapper converts gRPC InvocationResponse → HTTP response

### Test Infrastructure ✅
- Sample TodoAPI function app with 6 HTTP endpoints
- 8 comprehensive integration tests written
- IAsyncLifetime pattern for test setup/cleanup
- xUnit integration working

## 🔴 Current Blockers

### Function Loading/Discovery (Critical)

**Issue**: Functions are not being discovered/loaded by the worker, preventing invocation.

**Symptoms**:
```
Received 0 function(s) from worker
Status Code: InternalServerError
Content: Error invoking function: A task was canceled
```

**Technical Details**:
1. FunctionsMetadataRequest returns empty list
2. Worker needs function metadata to discover functions
3. Metadata typically comes from:
   - `.functions.json` files (generated at build time)
   - Embedded resources
   - Code generation by Azure Functions Worker SDK

4. Current state:
   - No `.functions.json` files in Sample.FunctionApp/bin output
   - Worker can't discover functions without metadata
   - Invocations fail with timeout/cancellation

**Suspected Root Causes**:
1. **Missing Build Configuration**: Sample.FunctionApp might need special MSBuild properties to trigger metadata generation
2. **SDK Target Not Running**: Azure Functions Worker SDK has build targets that generate metadata, might not be executing
3. **Wrong Directory**: FunctionAppDirectory might not point to where metadata is expected
4. **Metadata Format**: Dotnet-isolated might use different metadata format than in-process

**Evidence**:
```powershell
# Checking Sample.FunctionApp output
Get-ChildItem samples/Sample.FunctionApp/bin/Debug/net8.0 -Recurse -Filter "*.json"
# Returns: extensions.json, host.json, local.settings.json, worker.config.json
# Missing: function.json files or .azurefunctions/*.functions.json

# .azurefunctions folder exists but only contains DLLs:
samples/Sample.FunctionApp/bin/Debug/net8.0/.azurefunctions/
  - function.deps.json
  - Microsoft.Azure.Functions.Worker.Extensions.dll
  - (no function metadata JSON files)
```

**Next Steps** (In Priority Order):
1. ✅ **Research Worker SDK**: Examine `Microsoft.Azure.Functions.Worker.Sdk` source
   - Look at MSBuild targets for metadata generation
   - Check if there's a property to enable/disable metadata generation
   - See how official Azure Functions projects are configured

2. ✅ **Check Sample.FunctionApp .csproj**: Compare with working Azure Functions project
   - Ensure AzureFunctionsVersion is set
   - Check for any missing SDK references
   - Look for _FunctionsSkipCleanOutput or similar properties

3. ✅ **Alternative Discovery**: Consider using reflection-based discovery
   - Worker SDK has DefaultFunctionMetadataProvider
   - Might be able to use IFunctionMetadataProvider directly
   - Could bypass gRPC metadata request entirely

4. ✅ **Debug Worker Startup**: Add detailed logging to see what the worker is doing
   - Log what FunctionAppDirectory is set to
   - Log what metadata provider is being used
   - Capture any errors during function discovery

5. ✅ **Test with Real func.exe**: Run Sample.FunctionApp with func.exe to see what gets generated
   ```bash
   cd samples/Sample.FunctionApp
   func start
   # Check if metadata files appear
   ```

## 🟡 Known Issues (Non-Blocking)

### 1. Function Route Matching (Needs Work)
**Location**: `src/AzureFunctions.TestFramework.Core/Client/FunctionsHttpMessageHandler.cs`

**Issue**: Routes are hardcoded in FindFunctionIdFromRoute():
```csharp
var routeMap = new Dictionary<string, string>
{
    { "/api/todos", "GetTodos" },
    { "/api/todos/{id}", "GetTodo" },
    // ... hardcoded routes
};
```

**Solution**: Needs dynamic route discovery from function metadata once function loading works.

### 2. Disposal Warnings
**Symptoms**: During test cleanup, see warnings:
```
Error in event stream
System.IO.IOException: The request stream was aborted.
ConnectionAbortedException: The connection was aborted because the server is shutting down
```

**Impact**: Low - tests complete successfully, just noisy logs

**Solution**: Implement graceful shutdown:
- Stop accepting new requests before closing server
- Increase HostOptions.ShutdownTimeout
- Close worker connection cleanly before stopping server

### 3. Request ID Tracking
**Location**: `src/AzureFunctions.TestFramework.Core/Grpc/GrpcHostService.cs`

**Warning**: "Received response for unknown request: {guid}"

**Cause**: Timing issue where worker sends response for WorkerInitRequest before we're tracking it

**Solution**: Already mitigated by using Task.Run for async initialization. May need better request/response correlation.

### 4. DI Service Overrides Not Tested
**Status**: Infrastructure in place but not tested

**Location**: 
- `WorkerHostService.ConfigureServices()`
- `FunctionsTestHostBuilder.ConfigureServices()`

**Next Steps**: Once function invocation works, add tests for:
- Replacing services in DI container
- Verifying overridden services are used
- Scoped vs singleton behavior

### 5. Function Metadata Discovery
**Status**: Placeholder, needs implementation

**Issue**: FunctionsHttpMessageHandler needs function metadata to map routes to function IDs

**Depends On**: Function loading working first

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
dotnet test

# Run single test with detailed output
dotnet test --filter "GetTodos_ReturnsEmptyList" --logger "console;verbosity=detailed"

# Check function metadata files
Get-ChildItem samples/Sample.FunctionApp/bin/Debug/net8.0 -Recurse -Filter "*.json"

# Check for function discovery logs
dotnet test --filter "GetTodos_ReturnsEmptyList" 2>&1 | Select-String -Pattern "function"
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
- Azure Functions Worker: 1.23.0
- Grpc.AspNetCore: 2.68.0
- xUnit: 2.9.2

Last Updated: 2026-03-02
