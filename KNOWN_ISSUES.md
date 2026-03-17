# Known Issues and Current Status

## 🟢 What Works

### Core Infrastructure ✅
- Solution structure builds successfully
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
- Sample function app with HTTP endpoints (Todo CRUD + Health + Echo + Correlation + Configuration), HeartbeatTimer, ServiceBus trigger, Queue trigger, Blob trigger, and Event Grid trigger in `Sample.FunctionApp.Worker` (`net9.0`)
- **Worker SDK 2.x (net9.0)**: Integration tests in `Sample.FunctionApp.Worker.Tests` pass (FunctionsTestHost-based)
- **Worker SDK 2.x (net9.0)**: Integration tests in `Sample.FunctionApp.Worker.WAF.Tests` pass (FunctionsWebApplicationFactory-based)
- `CorrelationIdMiddleware` is covered end-to-end in both test projects; the `FunctionsTestHost` sample uses `WithHostBuilderFactory(Program.CreateHostBuilder)` and the WAF sample uses `FunctionsWebApplicationFactory<Program>`
- `FunctionsTestHost.Services` exposes the worker service provider after startup
- `FunctionsTestHostBuilder.ConfigureSetting()` overlays test-specific configuration values that functions can consume via `IConfiguration`
- `FunctionsTestHostBuilder.ConfigureEnvironmentVariable()` sets process-level environment variables visible to the worker via `IConfiguration` and `Environment.GetEnvironmentVariable()`
- Dedicated `FunctionsTestHost` tests now verify both inline service replacement and `WithHostBuilderFactory(Program.CreateWorkerHostBuilder)` service overrides
- `FunctionsTestHost` startup and `FunctionsWebApplicationFactory` readiness are event-driven (worker connection + function-load signals) rather than fixed-delay polling
- Direct gRPC HTTP dispatch precompiles route templates once per host, and `FunctionsTestHost.CreateHttpClient()` reuses host-local handlers
- `Sample.FunctionApp.Worker.Tests\SharedFunctionsTestHostFixture.cs` demonstrates an opt-in shared-host pattern for suites that can reset state between tests
- Durable spike support exists in separate projects: `AzureFunctions.TestFramework.Durable`, `Sample.FunctionApp.Durable`, and `Sample.FunctionApp.Durable.Tests`
- `Sample.FunctionApp.Durable.Tests` verifies durable metadata discovery, `[DurableClient]` HTTP starter execution, direct activity invocation, sub-orchestrator execution, fake custom-status visibility, and provider-driven orchestration completion fully in-process

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

### Trigger Invocations ✅
- **TimerTrigger** — `AzureFunctions.TestFramework.Timer`: `host.InvokeTimerAsync(name, timerInfo?)`
- **ServiceBusTrigger** — `AzureFunctions.TestFramework.ServiceBus`: `host.InvokeServiceBusAsync(name, ServiceBusMessage)`
- **QueueTrigger** — `AzureFunctions.TestFramework.Queue`: `host.InvokeQueueAsync(name, QueueMessage)`
- **BlobTrigger** — `AzureFunctions.TestFramework.Blob`: `host.InvokeBlobAsync(name, BinaryData, blobName?, containerName?)`
- **EventGridTrigger** — `AzureFunctions.TestFramework.EventGrid`: `host.InvokeEventGridAsync(name, EventGridEvent)` and `host.InvokeEventGridAsync(name, CloudEvent)`

## 🔴 Current Blockers

- No active blockers for the current Worker SDK 2.x sample/test suites.

## 🟡 Known Issues (Non-Blocking)

- `ConfigureEnvironmentVariable(name, value)` sets process-level environment variables which are shared across all parallel tests. Tests that use different values for the same variable name should run sequentially (place them in a separate xUnit collection).
- The durable spike currently uses a framework-owned fake path (`ConfigureFakeDurableSupport(...)` + `FunctionsDurableClientProvider`) instead of bootstrapping the real Durable runtime and execution engine.

## 🔵 Future Enhancements

### 1. Durable Functions
- Decide whether to keep the current fake-backed model as the primary durable testing story or add a separate real-runtime track later
- Support richer durable APIs on the fake path (external events, termination/suspend/resume, management payload helpers beyond the current status-query flow)

### 2. Output Bindings
Currently focused on trigger (input) invocations. Need to support surfacing output binding data to tests:
- Queue output bindings
- Blob output bindings
- Table output bindings
- Return value bindings

### 3. Middleware Scenarios
- Authorization middleware
- Exception handling middleware

### 4. Additional Binding Types
- Event Hubs trigger
- Cosmos DB trigger
- SignalR bindings

## Testing Commands

```bash
# Build solution
dotnet build --configuration Release

# Worker SDK 2.x gRPC tests (.NET 9)
dotnet test tests/Sample.FunctionApp.Worker.Tests --no-build --configuration Release

# Worker SDK 2.x WAF tests (.NET 9)
dotnet test tests/Sample.FunctionApp.Worker.WAF.Tests --no-build --configuration Release

# Durable Functions spike tests (.NET 9)
dotnet test tests/Sample.FunctionApp.Durable.Tests --configuration Release

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
