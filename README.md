# Azure Functions Test Framework

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![NuGet](https://img.shields.io/nuget/v/AzureFunctions.TestFramework.Core.svg)](https://www.nuget.org/packages/AzureFunctions.TestFramework.Core)

An integration testing framework for Azure Functions (dotnet-isolated) that provides a `TestServer`/`WebApplicationFactory`-like experience. Under the hood, the framework uses ASP.NET Core's `TestServer` for both the gRPC communication channel and the worker's HTTP server — no TCP ports are opened and no firewall rules are needed.

## Project Status: Preview (pre-1.0)

`FunctionsTestHost` — the single unified test host — is **fully functional** for the Worker SDK 2.x (.NET 10) samples and test suites. It supports both **direct gRPC mode** (`ConfigureFunctionsWorkerDefaults()`) and **ASP.NET Core integration mode** (`ConfigureFunctionsWebApplication()`), and works with both the classic `IHostBuilder` API and the newer `IHostApplicationBuilder` / `FunctionsApplicationBuilder` API introduced in Worker SDK 2.x. No active blockers.

### Latest update (2026-05-24)

- Added `AzureFunctions.TestFramework.Warmup` with `InvokeWarmupAsync(...)` for `[WarmupTrigger]` functions, including 4-flavour matrix coverage.
- Added `AzureFunctions.TestFramework.Kafka` with `InvokeKafkaAsync(...)` and `InvokeKafkaBatchAsync(...)` — full trigger invocation support for `string`, `byte[]`, `KafkaRecord`, and JSON POCO parameter types; batch overloads for `IsBatched = true`; `[KafkaOutput]` captured via Core generically.
- Library coverage work completed for the framework solution: all `AzureFunctions.TestFramework.*` libraries are now at **80%+ line coverage** in the CI coverage report.
- Coverage reporting now excludes generated `obj` files (`-filefilters:-*/obj/*`) so metrics reflect maintainable source code rather than generated protobuf artifacts.
- New unit tests were added for Dapr builder extensions, CosmosDB/SQL builder and synthetic binding providers, Service Bus fake action/converter helpers, and additional Durable utility/configuration paths.
- Added `AzureFunctions.TestFramework.DataExplorer` with `[KustoInput]` synthetic input support (`WithKustoInputRows` / `WithKustoInputJson`) and verified `[KustoOutput]` capture across the 4-flavour matrix.

### Capabilities

| Area | Status |
|------|--------|
| **HTTP invocation** (GET / POST / PUT / PATCH / DELETE / HEAD / OPTIONS) | ✅ Both direct gRPC and ASP.NET Core integration modes |
| **`BindingContext.BindingData` from HTTP request** | ✅ JSON body top-level properties, `Query`, and `Headers` populated — matches real Azure Functions host behavior |
| **Trigger packages + binding helper packages** (Timer, Warmup, Queue, ServiceBus, Blob, EventGrid, EventHubs, CosmosDB, SQL, SignalR, MCP, Redis, RabbitMQ, Kafka, DataExplorer) | ✅ Extension methods + result capture |
| **Table input bindings** (`[TableInput]`) | ✅ `WithTableEntity` / `WithTableEntities` via `ISyntheticBindingProvider` |
| **CosmosDB input bindings** (`[CosmosDBInput]`) | ✅ `WithCosmosDBInputDocuments` via `ISyntheticBindingProvider` |
| **SQL input bindings** (`[SqlInput]`) | ✅ `WithSqlInputRows` via `ISyntheticBindingProvider` |
| **Redis input bindings** (`[RedisInput]`) | ✅ `WithRedisInput` via `ISyntheticBindingProvider` |
| **SignalR input bindings** (`[SignalRConnectionInfoInput]`, `[SignalREndpointsInput]`, `[SignalRNegotiationInput]`) | ✅ `WithSignalRConnectionInfo` / `WithSignalREndpoints` / `WithSignalRNegotiation` via `ISyntheticBindingProvider` |
| **Durable Functions** (starter, orchestrator, activity, sub-orchestrator, external events, orchestration-to-orchestration `SendEvent`) | ✅ Fake-backed in-process |
| **Durable entity APIs** (`GetEntityAsync` non-generic, `GetAllEntitiesAsync`, `CleanEntityStorageAsync`, entity→orchestration scheduling, orchestration entity locks) | ✅ Supported in fake durable client/runner |
| **Durable orchestration query API** (`GetAllInstancesAsync` with query filters) | ✅ Supported in fake durable client |
| **ASP.NET Core integration** (`ConfigureFunctionsWebApplication`) | ✅ Full parameter binding incl. `HttpRequest`, `FunctionContext`, typed route params, `CancellationToken` |
| **`WithHostBuilderFactory` + `ConfigureServices`** (`IHostBuilder`) | ✅ DI overrides, inherited app services |
| **`WithHostApplicationBuilderFactory`** (`FunctionsApplicationBuilder`) | ✅ Support for the modern `FunctionsApplication.CreateBuilder()` startup style |
| **Custom route prefixes** | ✅ Auto-detected from `host.json` |
| **Middleware testing** | ✅ End-to-end in both modes |
| **Output binding capture** | ✅ `ReadReturnValueAs<T>()`, `ReadOutputAs<T>(bindingName)` |
| **Service access / configuration overrides** | ✅ `Services`, `ConfigureSetting`, `ConfigureEnvironmentVariable` |
| **Worker-side logging** | ✅ `ConfigureWorkerLogging` routes function `ILogger` output to test output |
| **Metadata discovery** | ✅ `IFunctionsTestHost.GetFunctions()` |
| **NuGet packaging** | ✅ `net8.0;net10.0`, Source Link, symbol packages, central package management |
| **CI** | ✅ xUnit + NUnit + TUnit, push + PR |


## Goals

This framework aims to provide:
- **In-process testing**: No func.exe, no external processes, no open TCP ports — everything runs in-memory via ASP.NET Core `TestServer`
- **Fast execution**: Similar performance to ASP.NET Core TestServer
- **Single unified test host**: `FunctionsTestHost` handles both direct gRPC mode and ASP.NET Core integration mode
- **Full DI control**: Override services for testing
- **Middleware support**: Test middleware registered in `Program.cs`

## NuGet packages

| Package | Description | README |
|---------|-------------|--------|
| [`AzureFunctions.TestFramework.Core`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Core) | gRPC-based in-process test host, worker hosting, metadata inspection, shared invocation result types, `ISyntheticBindingProvider` extensibility | [README](src/AzureFunctions.TestFramework.Core/README.md) |
| [`AzureFunctions.TestFramework.Http`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Http) | HTTP client support (`CreateHttpClient()`), request/response mapping, forwarding handlers for both modes | [README](src/AzureFunctions.TestFramework.Http/README.md) |
| [`AzureFunctions.TestFramework.Timer`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Timer) | `InvokeTimerAsync(...)` | [README](src/AzureFunctions.TestFramework.Timer/README.md) |
| [`AzureFunctions.TestFramework.Warmup`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Warmup) | `InvokeWarmupAsync(...)` | [README](src/AzureFunctions.TestFramework.Warmup/README.md) |
| [`AzureFunctions.TestFramework.Queue`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Queue) | `InvokeQueueAsync(...)` for `string` and `QueueMessage` parameter types | [README](src/AzureFunctions.TestFramework.Queue/README.md) |
| [`AzureFunctions.TestFramework.ServiceBus`](https://www.nuget.org/packages/AzureFunctions.TestFramework.ServiceBus) | `InvokeServiceBusAsync(...)`, `InvokeServiceBusBatchAsync(...)`, `ConfigureFakeServiceBusMessageActions()` | [README](src/AzureFunctions.TestFramework.ServiceBus/README.md) |
| [`AzureFunctions.TestFramework.Blob`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Blob) | `InvokeBlobAsync(...)`, `WithBlobInputContent(...)`, `WithBlobServiceClient(...)` + `WithBlobInputClient(...)` for `BlobClient` types | [README](src/AzureFunctions.TestFramework.Blob/README.md) |
| [`AzureFunctions.TestFramework.EventGrid`](https://www.nuget.org/packages/AzureFunctions.TestFramework.EventGrid) | `InvokeEventGridAsync(...)` for both `EventGridEvent` and `CloudEvent` | [README](src/AzureFunctions.TestFramework.EventGrid/README.md) |
| [`AzureFunctions.TestFramework.EventHubs`](https://www.nuget.org/packages/AzureFunctions.TestFramework.EventHubs) | `InvokeEventHubAsync(...)` for single event, `InvokeEventHubBatchAsync(...)` for batch-trigger functions | [README](src/AzureFunctions.TestFramework.EventHubs/README.md) |
| [`AzureFunctions.TestFramework.CosmosDB`](https://www.nuget.org/packages/AzureFunctions.TestFramework.CosmosDB) | `InvokeCosmosDBAsync(...)` for change-feed trigger, `WithCosmosDBInputDocuments(...)` for input binding injection | [README](src/AzureFunctions.TestFramework.CosmosDB/README.md) |
| [`AzureFunctions.TestFramework.Sql`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Sql) | `InvokeSqlAsync(...)` for change-tracking trigger, `WithSqlInputRows(...)` for input binding injection | [README](src/AzureFunctions.TestFramework.Sql/README.md) |
| [`AzureFunctions.TestFramework.DataExplorer`](https://www.nuget.org/packages/AzureFunctions.TestFramework.DataExplorer) | `WithKustoInputRows(...)` / `WithKustoInputJson(...)` for `[KustoInput]` binding injection; `[KustoOutput]` captured via Core | [README](src/AzureFunctions.TestFramework.DataExplorer/README.md) |
| [`AzureFunctions.TestFramework.Tables`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Tables) | `WithTableEntity(...)`, `WithTableEntities(...)` (input binding injection); `[TableOutput]` capture works generically via Core | [README](src/AzureFunctions.TestFramework.Tables/README.md) |
| [`AzureFunctions.TestFramework.SignalR`](https://www.nuget.org/packages/AzureFunctions.TestFramework.SignalR) | `InvokeSignalRAsync(...)` for `[SignalRTrigger]`; `WithSignalRConnectionInfo(...)`, `WithSignalRNegotiation(...)`, `WithSignalREndpoints(...)` for input binding injection; `[SignalROutput]` captured via Core | [README](src/AzureFunctions.TestFramework.SignalR/README.md) |
| [`AzureFunctions.TestFramework.Durable`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Durable) | Fake-backed durable helpers, `ConfigureFakeDurableSupport(...)`, `FakeDurableTaskClient`, activity invocation, external events | [README](src/AzureFunctions.TestFramework.Durable/README.md) |
| [`AzureFunctions.TestFramework.Mcp`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Mcp) | `InvokeMcpToolAsync(...)`, `InvokeMcpResourceAsync(...)`, `InvokeMcpPromptAsync(...)` for MCP (Model Context Protocol) triggers | [README](src/AzureFunctions.TestFramework.Mcp/README.md) |
| [`AzureFunctions.TestFramework.Redis`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Redis) | `InvokeRedisPubSubAsync(...)`, `InvokeRedisListAsync(...)`, `InvokeRedisStreamAsync(...)` for Redis triggers; `WithRedisInput(...)` for `[RedisInput]` binding injection; `[RedisOutput]` captured via Core | [README](src/AzureFunctions.TestFramework.Redis/README.md) |
| [`AzureFunctions.TestFramework.RabbitMQ`](https://www.nuget.org/packages/AzureFunctions.TestFramework.RabbitMQ) | `InvokeRabbitMQAsync(...)` for `string`, `byte[]` (UTF-8 body), and JSON POCO trigger parameters; optional `RabbitMqTriggerMessageProperties` for trigger metadata; named `[RabbitMQOutput]` payloads via `OutputData` / `ReadOutputAs<T>(...)` | [README](src/AzureFunctions.TestFramework.RabbitMQ/README.md) |
| [`AzureFunctions.TestFramework.Kafka`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Kafka) | `InvokeKafkaAsync(...)` for `string`, `byte[]`, `KafkaRecord`, and JSON POCO trigger parameters; `InvokeKafkaBatchAsync(...)` for all batched variants (`IsBatched = true`); `[KafkaOutput]` captured via Core | [README](src/AzureFunctions.TestFramework.Kafka/README.md) |

## Project setup requirements

### ASP.NET Core shared framework reference

If your function app uses `ConfigureFunctionsWebApplication()` (i.e., it references `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore`), it must declare a framework reference to `Microsoft.AspNetCore.App`:

```xml
<!-- YourFunctionApp.csproj -->
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
```

> ℹ️ You do **not** need to add `FrameworkReference` to your test project manually; it is propagated through the test framework's NuGet package metadata.

See the [Core package README](src/AzureFunctions.TestFramework.Core/README.md) for more details.

## Common commands

```bash
# Build solution
dotnet build --configuration Release

# Run all tests
dotnet test --configuration Release

# 4-flavour test matrix (IHostBuilder / IHostApplicationBuilder × direct gRPC / ASP.NET Core)
dotnet test tests/TestProject.HostBuilder.Tests --no-build --configuration Release
dotnet test tests/TestProject.HostBuilder.AspNetCore.Tests --no-build --configuration Release
dotnet test tests/TestProject.HostApplicationBuilder.Tests --no-build --configuration Release
dotnet test tests/TestProject.HostApplicationBuilder.AspNetCore.Tests --no-build --configuration Release

# Custom route prefix tests (4-flavour)
dotnet test tests/TestProject.CustomRoutePrefix.HostBuilder.Tests --no-build --configuration Release
dotnet test tests/TestProject.CustomRoutePrefix.HostBuilder.AspNetCore.Tests --no-build --configuration Release
dotnet test tests/TestProject.CustomRoutePrefix.HostApplicationBuilder.Tests --no-build --configuration Release
dotnet test tests/TestProject.CustomRoutePrefix.HostApplicationBuilder.AspNetCore.Tests --no-build --configuration Release

# Sample tests (Worker SDK 2.x, Durable, Custom route prefix)
dotnet test samples/Sample.FunctionApp.Worker.Tests --no-build --configuration Release
dotnet test samples/Sample.FunctionApp.Durable.Tests --no-build --configuration Release
dotnet test samples/Sample.FunctionApp.CustomRoutePrefix.Tests --no-build --configuration Release
dotnet test samples/Sample.FunctionApp.CustomRoutePrefix.AspNetCore.Tests --no-build --configuration Release

# Pack NuGet packages locally
dotnet pack --configuration Release --output ./artifacts
```

## Next likely areas

- Richer durable lifecycle helpers and pagination/continuation behavior parity
- Additional typed helpers for more complex output payloads
- More middleware scenarios such as authorization and exception handling
- More binding types such as Kafka and SendGrid

## Project Structure

```  
src/
  AzureFunctions.TestFramework.Core/         # gRPC host (TestServer-backed), worker hosting, in-memory invocation — both modes (net8.0;net10.0)
  AzureFunctions.TestFramework.Http/         # HTTP client support, request/response mapping, forwarding handlers (net8.0;net10.0)
  AzureFunctions.TestFramework.Timer/        # TimerTrigger invocation (net8.0;net10.0)
  AzureFunctions.TestFramework.Warmup/       # WarmupTrigger invocation (net8.0;net10.0)
  AzureFunctions.TestFramework.Queue/        # QueueTrigger invocation (net8.0;net10.0)
  AzureFunctions.TestFramework.ServiceBus/   # ServiceBusTrigger invocation (net8.0;net10.0)
  AzureFunctions.TestFramework.Blob/         # BlobTrigger invocation + BlobInput injection (net8.0;net10.0)
  AzureFunctions.TestFramework.EventGrid/    # EventGridTrigger invocation (net8.0;net10.0)
  AzureFunctions.TestFramework.EventHubs/    # EventHubTrigger invocation — single + batch (net8.0;net10.0)
  AzureFunctions.TestFramework.CosmosDB/     # CosmosDBTrigger invocation + CosmosDBInput injection (net8.0;net10.0)
  AzureFunctions.TestFramework.Sql/          # SqlTrigger invocation + SqlInput injection (net8.0;net10.0)
  AzureFunctions.TestFramework.DataExplorer/ # KustoInput injection for Azure Data Explorer + generic KustoOutput capture (net8.0;net10.0)
  AzureFunctions.TestFramework.Tables/       # TableInput injection via ISyntheticBindingProvider (net8.0;net10.0)
  AzureFunctions.TestFramework.SignalR/      # SignalRTrigger invocation + SignalR input binding injection (net8.0;net10.0)
  AzureFunctions.TestFramework.Redis/        # RedisPubSubTrigger/RedisListTrigger/RedisStreamTrigger invocation + RedisInput injection (net8.0;net10.0)
  AzureFunctions.TestFramework.Durable/      # Fake durable support (net8.0;net10.0)

samples/
  Sample.FunctionApp/                        # Minimal worker app (net10.0) — used by sample test projects
  Sample.FunctionApp.Tests.XUnit/            # xUnit sample test project
  Sample.FunctionApp.Tests.NUnit/            # NUnit sample test project
  Sample.FunctionApp.Tests.TUnit/            # TUnit sample test project
  Sample.FunctionApp.Worker/                 # Worker SDK 2.x (net10.0) — TodoAPI, middleware, triggers
  Sample.FunctionApp.Worker.Tests/           # xUnit — both direct gRPC and ASP.NET Core integration mode (~7 tests)
  Sample.FunctionApp.Durable/               # Durable Functions sample — HTTP starter + orchestrator + activity
  Sample.FunctionApp.Durable.Tests/          # xUnit — Durable Functions (~25 tests)
  Sample.FunctionApp.CustomRoutePrefix/      # Custom route prefix with ConfigureFunctionsWorkerDefaults()
  Sample.FunctionApp.CustomRoutePrefix.Tests/              # xUnit — custom prefix via direct gRPC (2 tests)
  Sample.FunctionApp.CustomRoutePrefix.AspNetCore/         # Custom route prefix with ConfigureFunctionsWebApplication()
  Sample.FunctionApp.CustomRoutePrefix.AspNetCore.Tests/   # xUnit — custom prefix via ASP.NET Core integration (3 tests)

tests/
  # 4-flavour test matrix — all share logic from tests/Shared/
  TestProject.HostBuilder/                              # Function app — IHostBuilder, ConfigureFunctionsWorkerDefaults
  TestProject.HostBuilder.Tests/                        # xUnit — direct gRPC, IHostBuilder
  TestProject.HostBuilder.AspNetCore/                   # Function app — IHostBuilder, ConfigureFunctionsWebApplication
  TestProject.HostBuilder.AspNetCore.Tests/             # xUnit — ASP.NET Core integration, IHostBuilder
  TestProject.HostApplicationBuilder/                   # Function app — FunctionsApplicationBuilder (direct gRPC)
  TestProject.HostApplicationBuilder.Tests/             # xUnit — direct gRPC, FunctionsApplicationBuilder
  TestProject.HostApplicationBuilder.AspNetCore/        # Function app — FunctionsApplicationBuilder + ConfigureFunctionsWebApplication
  TestProject.HostApplicationBuilder.AspNetCore.Tests/  # xUnit — ASP.NET Core integration, FunctionsApplicationBuilder
  # Custom route prefix 4-flavour matrix (one test project per function app)
  TestProject.CustomRoutePrefix.HostBuilder/            # CRP function app — IHostBuilder, gRPC
  TestProject.CustomRoutePrefix.HostBuilder.Tests/
  TestProject.CustomRoutePrefix.HostBuilder.AspNetCore/ # CRP function app — IHostBuilder, ASP.NET Core
  TestProject.CustomRoutePrefix.HostBuilder.AspNetCore.Tests/
  TestProject.CustomRoutePrefix.HostApplicationBuilder/            # CRP function app — FunctionsApplicationBuilder, gRPC
  TestProject.CustomRoutePrefix.HostApplicationBuilder.Tests/
  TestProject.CustomRoutePrefix.HostApplicationBuilder.AspNetCore/ # CRP function app — FunctionsApplicationBuilder, ASP.NET Core
  TestProject.CustomRoutePrefix.HostApplicationBuilder.AspNetCore.Tests/
  Shared/                                               # Shared test base classes + shared function implementations
  TestProject.Shared/                                   # Shared project consumed by all test projects
```

## Building

```bash
dotnet restore
dotnet build
```

## Testing

```bash
# All tests
dotnet test

# 4-flavour test matrix
dotnet test tests/TestProject.HostBuilder.Tests
dotnet test tests/TestProject.HostBuilder.AspNetCore.Tests
dotnet test tests/TestProject.HostApplicationBuilder.Tests
dotnet test tests/TestProject.HostApplicationBuilder.AspNetCore.Tests

# Custom route prefix tests (4-flavour)
dotnet test tests/TestProject.CustomRoutePrefix.HostBuilder.Tests
dotnet test tests/TestProject.CustomRoutePrefix.HostBuilder.AspNetCore.Tests
dotnet test tests/TestProject.CustomRoutePrefix.HostApplicationBuilder.Tests
dotnet test tests/TestProject.CustomRoutePrefix.HostApplicationBuilder.AspNetCore.Tests

# Sample tests (Worker SDK 2.x, Durable, Custom route prefix)
dotnet test samples/Sample.FunctionApp.Worker.Tests
dotnet test samples/Sample.FunctionApp.Durable.Tests
dotnet test samples/Sample.FunctionApp.CustomRoutePrefix.Tests
dotnet test samples/Sample.FunctionApp.CustomRoutePrefix.AspNetCore.Tests

# Single test with detailed logging
dotnet test --filter "GetTodos_ReturnsEmptyList" --logger "console;verbosity=detailed"
```

## Known Issues

See [KNOWN_ISSUES.md](KNOWN_ISSUES.md) for active caveats.

## References

- [Azure Functions Worker SDK](https://github.com/Azure/azure-functions-dotnet-worker)
- [Azure Functions RPC Protocol](https://github.com/Azure/azure-functions-language-worker-protobuf)

## License

MIT
