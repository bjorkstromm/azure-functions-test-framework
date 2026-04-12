# Extension Support Plan

Support every built-in extension from [Azure Functions Isolated Worker](https://github.com/Azure/azure-functions-dotnet-worker/tree/main/extensions).

> **Note on output bindings:** All output bindings (`[QueueOutput]`, `[BlobOutput]`, `[TableOutput]`, `[ServiceBusOutput]`, `[EventGridOutput]`, etc.) are captured **generically** by Core's `FunctionInvocationResult.OutputData`. No per-extension output binding code is needed — they work today for any function invoked through the framework.

## Current State

### Already Supported — Detailed Binding Audit

#### `AzureFunctions.TestFramework.Http` ✅ Fully Covered

| Binding | Worker Extension | Test Framework | Status |
|---------|-----------------|----------------|--------|
| `[HttpTrigger]` (trigger) | ✅ | ✅ `CreateHttpClient()` | ✅ |
| `[FromBody]` (input) | ✅ | ✅ ASP.NET Core integration mode only | ⚠️ |
| `[HttpResult]` (output) | ✅ | ✅ HTTP response returned via HttpClient | ✅ |

> **Note:** `[FromBody]` only works in **ASP.NET Core integration mode**. In direct gRPC mode, the Worker SDK's `DefaultFromBodyConversionFeature` requires `NullableHeaders` in the proto definition, which is not yet included in the framework's proto. Use `req.ReadFromJsonAsync<T>()` as an alternative in direct gRPC mode.

#### `AzureFunctions.TestFramework.Timer` ✅ Fully Covered

| Binding | Worker Extension | Test Framework | Status |
|---------|-----------------|----------------|--------|
| `[TimerTrigger]` (trigger) | ✅ | ✅ `InvokeTimerAsync()` | ✅ |

Timer has only a trigger. No input/output bindings exist in the worker extension.

#### `AzureFunctions.TestFramework.Queue` ✅ Fully Covered

| Binding | Worker Extension | Test Framework | Status |
|---------|-----------------|----------------|--------|
| `[QueueTrigger]` (trigger) | ✅ | ✅ `InvokeQueueAsync()` | ✅ |
| `[QueueOutput]` (output) | ✅ | ✅ Generic output capture | ✅ |

#### `AzureFunctions.TestFramework.ServiceBus` ✅ Fully Covered

| Binding | Worker Extension | Test Framework | Status |
|---------|-----------------|----------------|--------|
| `[ServiceBusTrigger]` — single message (`string`/`byte[]`/`BinaryData`) | ✅ | ✅ `InvokeServiceBusAsync(ServiceBusMessage)` | ✅ |
| `[ServiceBusTrigger]` — single message (`ServiceBusReceivedMessage`) | ✅ | ✅ `InvokeServiceBusAsync(ServiceBusReceivedMessage)` | ✅ |
| `[ServiceBusTrigger]` — **batch mode** (`ServiceBusReceivedMessage[]`) | ✅ | ✅ `InvokeServiceBusBatchAsync(IReadOnlyList<ServiceBusReceivedMessage>)` | ✅ |
| `[ServiceBusOutput]` (output) | ✅ | ✅ Generic output capture | ✅ |
| `ServiceBusMessageActions` (SDK-injected) | ✅ | ✅ `FakeServiceBusMessageActions` via `ConfigureFakeServiceBusMessageActions()` | ✅ |
| `ServiceBusSessionMessageActions` (SDK-injected) | ✅ | ✅ `FakeServiceBusSessionMessageActions` via `ConfigureFakeServiceBusMessageActions()` | ✅ |

#### `AzureFunctions.TestFramework.Blob` ✅ Fully Covered

| Binding | Worker Extension | Test Framework | Status |
|---------|-----------------|----------------|--------|
| `[BlobTrigger]` (trigger) | ✅ | ✅ `InvokeBlobAsync()` | ✅ |
| `[BlobInput]` (input) | ✅ | ✅ `WithBlobInputContent()` via `BlobInputSyntheticBindingProvider` | ✅ |
| `[BlobOutput]` (output) | ✅ | ✅ Generic output capture | ✅ |

> **`[BlobInput]` scope:** `WithBlobInputContent(blobPath, BinaryData)` injects bytes for parameters typed as `string`, `byte[]`, `Stream`, `BinaryData`, or `ReadOnlyMemory<byte>`. For complex SDK client types (`BlobClient`, `BlockBlobClient`, etc.) that use model-binding-data payloads, override the Azure Blob SDK client in DI via `ConfigureServices` instead.

#### `AzureFunctions.TestFramework.EventGrid` ✅ Fully Covered

| Binding | Worker Extension | Test Framework | Status |
|---------|-----------------|----------------|--------|
| `[EventGridTrigger]` — `EventGridEvent` | ✅ | ✅ `InvokeEventGridAsync(EventGridEvent)` | ✅ |
| `[EventGridTrigger]` — `CloudEvent` | ✅ | ✅ `InvokeEventGridAsync(CloudEvent)` | ✅ |
| `[EventGridOutput]` (output) | ✅ | ✅ Generic output capture | ✅ |

#### `AzureFunctions.TestFramework.EventHubs` ✅ Fully Covered

| Binding | Worker Extension | Test Framework | Status |
|---------|-----------------|----------------|--------|
| `[EventHubTrigger]` — single event (`EventData`, `IsBatched = false`) | ✅ | ✅ `InvokeEventHubAsync(EventData)` | ✅ |
| `[EventHubTrigger]` — **batch mode** (`EventData[]`, default `IsBatched = true`) | ✅ | ✅ `InvokeEventHubBatchAsync(IReadOnlyList<EventData>)` | ✅ |
| `[EventHubOutput]` (output) | ✅ | ✅ Generic output capture | ✅ |

#### `AzureFunctions.TestFramework.CosmosDB` ✅ Fully Covered

| Binding | Worker Extension | Test Framework | Status |
|---------|-----------------|----------------|--------|
| `[CosmosDBTrigger]` — change-feed batch (`IReadOnlyList<T>`) | ✅ | ✅ `InvokeCosmosDBAsync<T>(IReadOnlyList<T>)` | ✅ |
| `[CosmosDBTrigger]` — raw JSON string | ✅ | ✅ `InvokeCosmosDBAsync(string documentsJson)` | ✅ |
| `[CosmosDBInput]` (input) | ✅ | ✅ `WithCosmosDBInputDocuments(...)` via `CosmosDBInputSyntheticBindingProvider` | ✅ |
| `[CosmosDBOutput]` (output) | ✅ | ✅ Generic output capture | ✅ |

> **`[CosmosDBInput]` scope:** `WithCosmosDBInputDocuments(databaseName, containerName, document)` injects a single document or list of documents for parameters typed as POCO types or `string`. The key is `"{databaseName}/{containerName}"` (case-insensitive). For complex SDK client types (`CosmosClient`, `Container`, etc.), override via `ConfigureServices` instead.

#### `AzureFunctions.TestFramework.Tables` ✅ Fully Covered

| Binding | Worker Extension | Test Framework | Status |
|---------|-----------------|----------------|--------|
| `[TableInput]` — single entity (POCO / `ITableEntity`) | ✅ | ✅ `WithTableEntity(tableName, pk, rk, entity)` | ✅ |
| `[TableInput]` — collection (`IEnumerable<T>`) | ✅ | ✅ `WithTableEntities(tableName, entities)` | ✅ |
| `[TableInput]` — partition collection | ✅ | ✅ `WithTableEntities(tableName, pk, entities)` | ✅ |
| `[TableOutput]` (output) | ✅ | ✅ Generic output capture | ✅ |

> **Note:** Tables has no trigger. `[TableInput]` with `TableClient` parameters is not supported by `WithTableEntity` / `WithTableEntities` (uses model-binding-data; override via `ConfigureServices` instead).

#### `AzureFunctions.TestFramework.Durable` ✅ Fully Covered

Not a built-in extension (separate NuGet: `Microsoft.Azure.Functions.Worker.Extensions.DurableTask`), but fully supported with fake client, orchestration context, entity support, and `ISyntheticBindingProvider`.

### Not Yet Supported

| Extension | NuGet Package | Trigger | Input | Output |
|-----------|---------------|---------|-------|--------|
| **SignalR Service** | `Microsoft.Azure.Functions.Worker.Extensions.SignalRService` | `[SignalRTrigger]` | `[SignalRConnectionInfo]`, `[SignalREndpoints]`, `[SignalRNegotiation]` | `[SignalROutput]` |
| **Kafka** | `Microsoft.Azure.Functions.Worker.Extensions.Kafka` | `[KafkaTrigger]` | — | `[KafkaOutput]` |
| **RabbitMQ** | `Microsoft.Azure.Functions.Worker.Extensions.RabbitMQ` | `[RabbitMQTrigger]` | — | `[RabbitMQOutput]` |
| **SendGrid** | `Microsoft.Azure.Functions.Worker.Extensions.SendGrid` | — | — | `[SendGrid]` |
| **Warmup** | `Microsoft.Azure.Functions.Worker.Extensions.Warmup` | `[WarmupTrigger]` | — | — |

### Infrastructure-only (no user-facing bindings — no action needed)

- `Worker.Extensions.Abstractions` — base attribute classes
- `Worker.Extensions.Shared` — internal shared utilities
- `Worker.Extensions.Rpc` — gRPC extension plumbing
- `Worker.Extensions.Storage` — meta-package referencing Blobs + Queues
- `Worker.Extensions.Http.AspNetCore` — ASP.NET Core integration infrastructure

---

## Issues

### ~~Issue 1: CosmosDB Trigger, Input & Output bindings~~ ✅ Done

**Package:** `AzureFunctions.TestFramework.CosmosDB` — shipped.

See the "Already Supported" section above for the full binding audit. Key facts:
- `InvokeCosmosDBAsync<T>(functionName, IReadOnlyList<T> documents)` — strongly-typed change-feed trigger
- `InvokeCosmosDBAsync(functionName, string documentsJson)` — raw JSON change-feed trigger
- `WithCosmosDBInputDocuments(databaseName, containerName, document)` — injects a single document for `[CosmosDBInput]`
- `WithCosmosDBInputDocuments(databaseName, containerName, IReadOnlyList<T>)` — injects a list of documents
- `WithCosmosDBInputJson(databaseName, containerName, json)` — injects raw JSON for `[CosmosDBInput]`
- `[CosmosDBOutput]` captured generically by `FunctionInvocationResult.OutputData` or `ReadReturnValueAs<T>()`
- Tested across 4-flavour matrix: `IHostBuilder`×gRPC, `IHostBuilder`×ASP.NET Core, `FunctionsApplicationBuilder`×gRPC, `FunctionsApplicationBuilder`×ASP.NET Core

**NuGet dependency:** `Microsoft.Azure.Functions.Worker.Extensions.CosmosDB`

---

### ~~Issue 2: Event Hubs Trigger & Output binding~~ ✅ Done

**Package:** `AzureFunctions.TestFramework.EventHubs` — shipped.

See the "Already Supported" section above for the full binding audit. Key facts:
- `InvokeEventHubAsync(EventData)` — single-event trigger (`IsBatched = false`)
- `InvokeEventHubBatchAsync(IReadOnlyList<EventData>)` — batch-trigger (`IsBatched = true`, the default)
- `[EventHubOutput]` captured generically by `FunctionInvocationResult.OutputData` or `ReadReturnValueAs<T>()`
- Tested across 4-flavour matrix: `IHostBuilder`×gRPC, `IHostBuilder`×ASP.NET Core, `FunctionsApplicationBuilder`×gRPC, `FunctionsApplicationBuilder`×ASP.NET Core

**NuGet dependency:** `Microsoft.Azure.Functions.Worker.Extensions.EventHubs`

---

### ~~Issue 3: Table Input & Output bindings~~ ✅ Done

**Package:** `AzureFunctions.TestFramework.Tables` — shipped.

See the "Already Supported" section above for the full binding audit. Key facts:
- `WithTableEntity(tableName, pk, rk, entity)` — injects a single entity for `[TableInput("T", "pk", "rk")]`
- `WithTableEntities(tableName, entities)` — full-table collection for `[TableInput("T")]`
- `WithTableEntities(tableName, pk, entities)` — partition-scoped collection for `[TableInput("T", "pk")]`
- `[TableOutput]` captured generically by `FunctionInvocationResult.OutputData`
- Tested across 4-flavour matrix: `IHostBuilder`×gRPC, `IHostBuilder`×ASP.NET Core, `FunctionsApplicationBuilder`×gRPC, `FunctionsApplicationBuilder`×ASP.NET Core

**NuGet dependency:** `Microsoft.Azure.Functions.Worker.Extensions.Tables`

---

### Issue 4: SignalR Service Trigger, Input & Output bindings

**Package:** `AzureFunctions.TestFramework.SignalR`

**Bindings:**
- **Trigger:** `[SignalRTrigger]` — receives SignalR messages, connection, and disconnection events
- **Input:** `[SignalRConnectionInfo]` — gets client connection info for negotiation
- **Input:** `[SignalREndpoints]` — gets SignalR service endpoints
- **Input:** `[SignalRNegotiation]` — performs full SignalR negotiation
- **Output:** `[SignalROutput]` — sends messages/actions via SignalR

**Scope:**
- New package: `AzureFunctions.TestFramework.SignalR`
- Extension method: `InvokeSignalRAsync(this IFunctionsTestHost host, string functionName, InvocationContext invocationContext, ...)` — fires a SignalR trigger invocation
- Input bindings require `ISyntheticBindingProvider` implementations to inject fake connection info / negotiation results
- Output bindings captured via `FunctionInvocationResult.OutputData`
- Most complex extension — 1 trigger + 3 input bindings + 1 output binding
- Test across 4-flavour matrix

**NuGet dependency:** `Microsoft.Azure.Functions.Worker.Extensions.SignalRService`

---

### Issue 5: Kafka Trigger & Output binding

**Package:** `AzureFunctions.TestFramework.Kafka`

**Bindings:**
- **Trigger:** `[KafkaTrigger]` — receives events from Apache Kafka topics
- **Output:** `[KafkaOutput]` — sends events to Kafka topics

**Scope:**
- New package: `AzureFunctions.TestFramework.Kafka`
- Extension method: `InvokeKafkaAsync(this IFunctionsTestHost host, string functionName, ...)` — single event with key, value, headers, offset, partition, topic, timestamp
- Batch overload for batch trigger mode
- Output bindings captured via `FunctionInvocationResult.OutputData`
- Test across 4-flavour matrix

**NuGet dependency:** `Microsoft.Azure.Functions.Worker.Extensions.Kafka`

---

### Issue 6: RabbitMQ Trigger & Output binding

**Package:** `AzureFunctions.TestFramework.RabbitMQ`

**Bindings:**
- **Trigger:** `[RabbitMQTrigger]` — receives messages from RabbitMQ queues
- **Output:** `[RabbitMQOutput]` — sends messages to RabbitMQ exchanges

**Scope:**
- New package: `AzureFunctions.TestFramework.RabbitMQ`
- Extension method: `InvokeRabbitMQAsync(this IFunctionsTestHost host, string functionName, byte[] body, ...)` — single message with body and optional basic properties
- Output bindings captured via `FunctionInvocationResult.OutputData`
- Test across 4-flavour matrix

**NuGet dependency:** `Microsoft.Azure.Functions.Worker.Extensions.RabbitMQ`

---

### Issue 7: SendGrid Output binding

**Package:** `AzureFunctions.TestFramework.SendGrid`

**Bindings:**
- **Output:** `[SendGrid]` — sends emails via SendGrid

**Scope:**
- New package: `AzureFunctions.TestFramework.SendGrid`
- No trigger invocation (output-only binding)
- Output binding captured via `FunctionInvocationResult.OutputData` when used with other triggers (HTTP, Queue, Timer, etc.)
- Tests should demonstrate output binding capture for email-sending functions
- Test across 4-flavour matrix

**NuGet dependency:** `Microsoft.Azure.Functions.Worker.Extensions.SendGrid`

---

### Issue 8: Warmup Trigger

**Package:** `AzureFunctions.TestFramework.Warmup`

**Bindings:**
- **Trigger:** `[WarmupTrigger]` — fires when a new instance of the function app is warmed up

**Scope:**
- New package: `AzureFunctions.TestFramework.Warmup`
- Extension method: `InvokeWarmupAsync(this IFunctionsTestHost host, string functionName, WarmupContext? context = null, ...)` — triggers warmup function
- Simplest extension — no input data beyond optional `WarmupContext`, no output bindings beyond return value
- Test across 4-flavour matrix

**NuGet dependency:** `Microsoft.Azure.Functions.Worker.Extensions.Warmup`

---

## Implementation Notes

### Pattern for New Extension Packages

Each new package follows the established pattern (see existing Timer, Queue, Blob, etc.):

1. **Package structure:**
   - `AzureFunctions.TestFramework.{ExtensionName}/`
     - `AzureFunctions.TestFramework.{ExtensionName}.csproj` — targets `net8.0;net10.0`, references Core + the Worker extension NuGet package
     - `FunctionsTestHost{ExtensionName}Extensions.cs` — static extension class with `Invoke*Async` methods
   
2. **Extension method pattern:**
   ```
   InvokeXxxAsync(this IFunctionsTestHost host, string functionName, <trigger-specific-params>, CancellationToken cancellationToken = default)
   → Task<FunctionInvocationResult>
   ```

3. **Binding data factory:** Private static `CreateBindingData` method that converts trigger-specific params to `TriggerBindingData` with `FunctionBindingData`

4. **Output bindings:** Already captured generically by Core's `FunctionInvocationResult.OutputData` — no per-extension output binding work needed

5. **Input bindings:** For extensions with input bindings (CosmosDB, Tables, SignalR), implement `ISyntheticBindingProvider` to inject fake data, or document that users should override via `ConfigureServices`

6. **Testing:** All new features tested across the 4-flavour matrix. Shared test logic in `tests/Shared/Tests/` as abstract base classes.

### Suggested Priority

1. **CosmosDB** — Very high demand, commonly used with Azure Functions
2. **Event Hubs** — High demand for event-driven architectures
3. **SignalR** — Real-time scenarios, most complex
4. **Kafka** — Growing adoption
5. **RabbitMQ** — Niche but important
6. **SendGrid** — Output-only, low complexity
7. **Warmup** — Simplest, rarely tested in isolation
