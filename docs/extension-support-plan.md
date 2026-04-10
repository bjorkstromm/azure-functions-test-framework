# Extension Support Plan

Support every built-in extension from [Azure Functions Isolated Worker](https://github.com/Azure/azure-functions-dotnet-worker/tree/main/extensions).

> **Note on output bindings:** All output bindings (`[QueueOutput]`, `[BlobOutput]`, `[TableOutput]`, `[ServiceBusOutput]`, `[EventGridOutput]`, etc.) are captured **generically** by Core's `FunctionInvocationResult.OutputData`. No per-extension output binding code is needed — they work today for any function invoked through the framework.

## Current State

### Already Supported — Detailed Binding Audit

#### `AzureFunctions.TestFramework.Http` ✅ Fully Covered

| Binding | Worker Extension | Test Framework | Status |
|---------|-----------------|----------------|--------|
| `[HttpTrigger]` (trigger) | ✅ | ✅ `CreateHttpClient()` | ✅ |
| `[FromBody]` (input) | ✅ | ✅ Works naturally through HTTP body | ✅ |
| `[HttpResult]` (output) | ✅ | ✅ HTTP response returned via HttpClient | ✅ |

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

#### `AzureFunctions.TestFramework.ServiceBus` ⚠️ Partial

| Binding | Worker Extension | Test Framework | Status |
|---------|-----------------|----------------|--------|
| `[ServiceBusTrigger]` — single message | ✅ | ✅ `InvokeServiceBusAsync()` | ✅ |
| `[ServiceBusTrigger]` — **batch mode** | ✅ | ❌ No batch overload | ⚠️ Gap |
| `[ServiceBusOutput]` (output) | ✅ | ✅ Generic output capture | ✅ |
| `ServiceBusMessageActions` (SDK-injected) | ✅ | ❌ No fake provided | ⚠️ Gap |
| `ServiceBusSessionMessageActions` (SDK-injected) | ✅ | ❌ No fake provided | ⚠️ Gap |

**Gaps:**
- **Batch trigger mode**: No `InvokeServiceBusBatchAsync()` for `IsBatched = true`
- **`ServiceBusMessageActions`**: Functions can inject this to Complete/Abandon/DeadLetter messages. No fake is provided — users must mock via `ConfigureServices`.
- **`ServiceBusSessionMessageActions`**: Same for session-enabled queues/topics.

#### `AzureFunctions.TestFramework.Blob` ⚠️ Partial

| Binding | Worker Extension | Test Framework | Status |
|---------|-----------------|----------------|--------|
| `[BlobTrigger]` (trigger) | ✅ | ✅ `InvokeBlobAsync()` | ✅ |
| `[BlobInput]` (input) | ✅ | ❌ No support | ❌ Gap |
| `[BlobOutput]` (output) | ✅ | ✅ Generic output capture | ✅ |

**Gaps:**
- **`[BlobInput]`**: Used to read blobs as input parameters (e.g. `[BlobInput("container/{name}")] string content` or `Stream`/`BlobClient`). Needs `ISyntheticBindingProvider` for `"blobInput"` or user `ConfigureServices` override.

#### `AzureFunctions.TestFramework.EventGrid` ✅ Fully Covered

| Binding | Worker Extension | Test Framework | Status |
|---------|-----------------|----------------|--------|
| `[EventGridTrigger]` — `EventGridEvent` | ✅ | ✅ `InvokeEventGridAsync(EventGridEvent)` | ✅ |
| `[EventGridTrigger]` — `CloudEvent` | ✅ | ✅ `InvokeEventGridAsync(CloudEvent)` | ✅ |
| `[EventGridOutput]` (output) | ✅ | ✅ Generic output capture | ✅ |

#### `AzureFunctions.TestFramework.Durable` ✅ Fully Covered

Not a built-in extension (separate NuGet: `Microsoft.Azure.Functions.Worker.Extensions.DurableTask`), but fully supported with fake client, orchestration context, entity support, and `ISyntheticBindingProvider`.

### Gaps in Existing Extensions

#### Issue 0a: ServiceBus — Batch trigger + MessageActions support

**Scope:**
- Add `InvokeServiceBusBatchAsync(this IFunctionsTestHost host, string functionName, IReadOnlyList<ServiceBusReceivedMessage> messages, ...)` overload for batch-trigger functions
- Provide fake `ServiceBusMessageActions` (Complete/Abandon/DeadLetter/Defer/RenewLock) injectable via `ConfigureServices` or `ISyntheticBindingProvider`
- Optionally provide fake `ServiceBusSessionMessageActions` for session-enabled scenarios
- Test across 4-flavour matrix

#### Issue 0b: Blob — Add `[BlobInput]` input binding support

**Scope:**
- Add `ISyntheticBindingProvider` for `"blobInput"` binding type to inject fake blob data
- Or document pattern for users to override via `ConfigureServices` with fake `BlobClient`/`BlobContainerClient`
- Test across 4-flavour matrix

---

### Not Yet Supported

| Extension | NuGet Package | Trigger | Input | Output |
|-----------|---------------|---------|-------|--------|
| **CosmosDB** | `Microsoft.Azure.Functions.Worker.Extensions.CosmosDB` | `[CosmosDBTrigger]` | `[CosmosDBInput]` | `[CosmosDBOutput]` |
| **Event Hubs** | `Microsoft.Azure.Functions.Worker.Extensions.EventHubs` | `[EventHubTrigger]` | — | `[EventHubOutput]` |
| **Tables** | `Microsoft.Azure.Functions.Worker.Extensions.Tables` | — | `[TableInput]` | `[TableOutput]` |
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

### Issue 1: CosmosDB Trigger, Input & Output bindings

**Package:** `AzureFunctions.TestFramework.CosmosDB`

**Bindings:**
- **Trigger:** `[CosmosDBTrigger]` — receives a list of changed documents from a CosmosDB change feed
- **Input:** `[CosmosDBInput]` — reads documents from CosmosDB (point-read or query)
- **Output:** `[CosmosDBOutput]` — writes documents to CosmosDB

**Scope:**
- New package: `AzureFunctions.TestFramework.CosmosDB`
- Extension method: `InvokeCosmosDBAsync(this IFunctionsTestHost host, string functionName, IReadOnlyList<T> documents, ...)` — sends a batch of change-feed documents as JSON
- Output bindings captured via existing `FunctionInvocationResult.OutputData`
- Input binding support via `ISyntheticBindingProvider` to inject fake document data, or users override via `ConfigureServices`
- Test across 4-flavour matrix (IHostBuilder × direct gRPC, IHostBuilder × ASP.NET Core, FunctionsApplicationBuilder × direct gRPC, FunctionsApplicationBuilder × ASP.NET Core)
- Sample function + tests

**NuGet dependency:** `Microsoft.Azure.Functions.Worker.Extensions.CosmosDB`

---

### Issue 2: Event Hubs Trigger & Output binding

**Package:** `AzureFunctions.TestFramework.EventHubs`

**Bindings:**
- **Trigger:** `[EventHubTrigger]` — receives events from Azure Event Hubs (single or batch mode)
- **Output:** `[EventHubOutput]` — sends events to Event Hubs

**Scope:**
- New package: `AzureFunctions.TestFramework.EventHubs`
- Extension method: `InvokeEventHubAsync(this IFunctionsTestHost host, string functionName, EventData eventData, ...)` — single event
- Batch overload: `InvokeEventHubBatchAsync(...)` — sends multiple `EventData` for batch-trigger functions
- Output bindings captured via `FunctionInvocationResult.OutputData`
- Test across 4-flavour matrix
- Sample function + tests

**NuGet dependency:** `Microsoft.Azure.Functions.Worker.Extensions.EventHubs`

---

### Issue 3: Table Input & Output bindings

**Package:** `AzureFunctions.TestFramework.Tables`

**Bindings:**
- **Input:** `[TableInput]` — reads entities from Azure Table Storage / Cosmos DB Table API
- **Output:** `[TableOutput]` — writes entities to Table Storage

**Scope:**
- New package: `AzureFunctions.TestFramework.Tables`
- No trigger (Tables has no trigger) — used alongside HTTP, Queue, Timer, or other triggers
- Input binding support via `ISyntheticBindingProvider` to inject fake table entity data
- Output bindings captured via `FunctionInvocationResult.OutputData`
- Tests should demonstrate input/output bindings in combination with existing triggers
- Test across 4-flavour matrix

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
3. **Tables** — Common for simple storage, pairs with existing triggers
4. **SignalR** — Real-time scenarios, most complex
5. **Kafka** — Growing adoption
6. **RabbitMQ** — Niche but important
7. **SendGrid** — Output-only, low complexity
8. **Warmup** — Simplest, rarely tested in isolation
