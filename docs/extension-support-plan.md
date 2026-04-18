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

#### `AzureFunctions.TestFramework.SignalR` ✅ Fully Covered

| Binding | Worker Extension | Test Framework | Status |
|---------|-----------------|----------------|--------|
| `[SignalRTrigger]` (trigger) — message events | ✅ | ✅ `InvokeSignalRAsync(SignalRInvocationContext)` | ✅ |
| `[SignalRTrigger]` (trigger) — connection/disconnection events | ✅ | ✅ `InvokeSignalRAsync(SignalRInvocationContext)` | ✅ |
| `[SignalRConnectionInfoInput]` (input) | ✅ | ✅ `WithSignalRConnectionInfo(url, accessToken)` via `SignalRConnectionInfoSyntheticBindingProvider` | ✅ |
| `[SignalREndpointsInput]` (input) | ✅ | ✅ `WithSignalREndpoints(SignalREndpoint[])` via `SignalREndpointsSyntheticBindingProvider` | ✅ |
| `[SignalRNegotiationInput]` (input) | ✅ | ✅ `WithSignalRNegotiation(SignalRNegotiationContext)` via `SignalRNegotiationSyntheticBindingProvider` | ✅ |
| `[SignalROutput]` (output) | ✅ | ✅ Generic output capture | ✅ |

> **`[SignalROutput]` note:** `SignalRMessageAction` and `SignalRGroupAction` have multiple parameterized constructors (no `[JsonConstructor]`), so `ReadReturnValueAs<SignalRMessageAction>()` is not available directly. Read the return value as `JsonElement` and inspect properties via `GetProperty(...)` instead.

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

#### `AzureFunctions.TestFramework.Mcp` ✅ Fully Covered

| Binding | Worker Extension | Test Framework | Status |
|---------|-----------------|----------------|--------|
| `[McpToolTrigger]` (trigger) | ✅ | ✅ `InvokeMcpToolAsync(functionName, toolArguments?, toolName?, sessionId?)` | ✅ |
| `[McpResourceTrigger]` (trigger) | ✅ | ✅ `InvokeMcpResourceAsync(functionName, resourceUri, sessionId?)` | ✅ |
| `[McpPromptTrigger]` (trigger) | ✅ | ✅ `InvokeMcpPromptAsync(functionName, arguments?, promptName?, sessionId?)` | ✅ |

> **Note:** MCP triggers require `FunctionsMcpContextMiddleware` to populate `FunctionContext.Items` before the function body executes. The framework automatically invokes the extension startup code from the functions assembly (working around the SDK's `Assembly.GetEntryAssembly()` limitation in test runners). See `docs/Reflection.md` §§ 10–11 for details.

#### `AzureFunctions.TestFramework.Sql` ✅ Fully Covered

| Binding | Worker Extension | Test Framework | Status |
|---------|-----------------|----------------|--------|
| `[SqlTrigger]` — change-tracking batch (`IReadOnlyList<SqlChange<T>>`) | ✅ | ✅ `InvokeSqlAsync<T>(IReadOnlyList<SqlChange<T>>)` | ✅ |
| `[SqlTrigger]` — raw JSON string | ✅ | ✅ `InvokeSqlAsync(string changesJson)` | ✅ |
| `[SqlInput]` (input) | ✅ | ✅ `WithSqlInputRows(...)` via `SqlInputSyntheticBindingProvider` | ✅ |
| `[SqlOutput]` (output) | ✅ | ✅ Generic output capture | ✅ |

> **`[SqlInput]` scope:** `WithSqlInputRows(commandText, rows)` injects a list of rows for parameters typed as `IEnumerable<T>`. The key is the `commandText` value declared in the `[SqlInput]` attribute (case-insensitive). For raw JSON injection use `WithSqlInputJson(commandText, json)`. When using `InvokeSqlAsync(string changesJson)`, `SqlChangeOperation` values must be integers (0=Insert, 1=Update, 2=Delete).

#### `AzureFunctions.TestFramework.Redis` ✅ Fully Covered

| Binding | Worker Extension | Test Framework | Status |
|---------|-----------------|----------------|--------|
| `[RedisPubSubTrigger]` (trigger) | ✅ | ✅ `InvokeRedisPubSubAsync(functionName, channel, message)` | ✅ |
| `[RedisListTrigger]` (trigger) | ✅ | ✅ `InvokeRedisListAsync(functionName, key, value)` | ✅ |
| `[RedisStreamTrigger]` (trigger) | ✅ | ✅ `InvokeRedisStreamAsync(functionName, key, entries)` | ✅ |
| `[RedisInput]` (input) | ✅ | ✅ `WithRedisInput(command, value)` via `RedisInputSyntheticBindingProvider` | ✅ |
| `[RedisOutput]` (output) | ✅ | ✅ Generic output capture | ✅ |

> **`[RedisInput]` scope:** `WithRedisInput(command, value)` injects a string result for parameters typed as `string` or any type whose converter accepts a string value. The key is the full `command` string declared in the `[RedisInput]` attribute (case-insensitive), e.g. `"GET mykey"`. Use `WithRedisInputJson(command, json)` for pre-serialized JSON injection. The message/value/entries passed to the trigger invocation methods are delivered as `string` binding data; functions whose parameters are typed as `string` receive the raw value directly.
>
> **`[RedisStreamTrigger]` note:** `InvokeRedisStreamAsync` accepts `IReadOnlyList<KeyValuePair<string, string>>` entries and serializes them to a JSON array of `{"name":"…","value":"…"}` objects. Functions that receive `string` get the raw JSON; for other types the worker's Redis converter handles deserialization.
- Tested across 4-flavour matrix: `IHostBuilder`×gRPC, `IHostBuilder`×ASP.NET Core, `FunctionsApplicationBuilder`×gRPC, `FunctionsApplicationBuilder`×ASP.NET Core

**NuGet dependency:** `Microsoft.Azure.Functions.Worker.Extensions.Redis`

### Not Yet Supported

| Extension | NuGet Package | Trigger | Input | Output |
|-----------|---------------|---------|-------|--------|
| **Kafka** | `Microsoft.Azure.Functions.Worker.Extensions.Kafka` | `[KafkaTrigger]` | — | `[KafkaOutput]` |
| **RabbitMQ** | `Microsoft.Azure.Functions.Worker.Extensions.RabbitMQ` | `[RabbitMQTrigger]` | — | `[RabbitMQOutput]` |
| **SendGrid** | `Microsoft.Azure.Functions.Worker.Extensions.SendGrid` | — | — | `[SendGrid]` |
| **Warmup** | `Microsoft.Azure.Functions.Worker.Extensions.Warmup` | `[WarmupTrigger]` | — | — |
| **Azure Data Explorer** | `Microsoft.Azure.Functions.Worker.Extensions.Kusto` *(preview)* | — | `[KustoInput]` | `[KustoOutput]` |
| **Dapr** | `Microsoft.Azure.Functions.Worker.Extensions.Dapr` | `[DaprBindingTrigger]`, `[DaprServiceInvocationTrigger]`, `[DaprTopicTrigger]` | `[DaprStateInput]`, `[DaprSecretInput]` | `[DaprStateOutput]`, `[DaprInvokeOutput]`, `[DaprPublishOutput]`, `[DaprBindingOutput]` |

### Not Applicable — No Isolated Worker Support

| Extension | Reason |
|-----------|--------|
| **Twilio** | `Microsoft.Azure.WebJobs.Extensions.Twilio` — [no isolated worker model support](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-twilio) |
| **Mobile Apps** | Functions v1.x only — not supported in v4.x runtime |
| **Notification Hubs** | Functions v1.x only — not supported in v4.x runtime |

### Infrastructure-only (no user-facing bindings — no action needed)

- `Worker.Extensions.Abstractions` — base attribute classes
- `Worker.Extensions.Shared` — internal shared utilities
- `Worker.Extensions.Rpc` — gRPC extension plumbing
- `Worker.Extensions.Storage` — meta-package referencing Blobs + Queues
- `Worker.Extensions.Http.AspNetCore` — ASP.NET Core integration infrastructure
- **IoT Hub** — uses the Azure Event Hubs extension under the hood (`Microsoft.Azure.Functions.Worker.Extensions.EventHubs`); already covered by `AzureFunctions.TestFramework.EventHubs`

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

### ~~Issue 4: SignalR Service Trigger, Input & Output bindings~~ ✅ Done

**Package:** `AzureFunctions.TestFramework.SignalR` — shipped.

See the "Already Supported" section above for the full binding audit. Key facts:
- `InvokeSignalRAsync(functionName, SignalRInvocationContext)` — fires a `[SignalRTrigger]` invocation for messages, connections, and disconnection events
- `WithSignalRConnectionInfo(url, accessToken)` — injects fake URL + token for `[SignalRConnectionInfoInput]`
- `WithSignalRConnectionInfo(SignalRConnectionInfo)` — convenience overload taking the SDK type directly
- `WithSignalREndpoints(SignalREndpoint[])` — injects fake endpoints for `[SignalREndpointsInput]`
- `WithSignalRNegotiation(SignalRNegotiationContext)` — injects a fake negotiation context for `[SignalRNegotiationInput]`
- `[SignalROutput]` captured generically; read as `JsonElement` due to `SignalRMessageAction` having multiple parameterized constructors
- Tested across 4-flavour matrix: `IHostBuilder`×gRPC, `IHostBuilder`×ASP.NET Core, `FunctionsApplicationBuilder`×gRPC, `FunctionsApplicationBuilder`×ASP.NET Core

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

### ~~Issue 9: Azure SQL Trigger, Input & Output bindings~~ ✅ Done

**Package:** `AzureFunctions.TestFramework.Sql` — shipped.

See the "Already Supported" section above for the full binding audit. Key facts:
- `InvokeSqlAsync<T>(functionName, IReadOnlyList<SqlChange<T>> changes)` — strongly-typed SQL change-tracking trigger
- `InvokeSqlAsync(functionName, string changesJson)` — raw JSON SQL trigger; enum values must be integers (0=Insert, 1=Update, 2=Delete)
- `WithSqlInputRows(commandText, row)` — injects a single row for `[SqlInput(commandText: "...")]`
- `WithSqlInputRows(commandText, IReadOnlyList<T> rows)` — injects a list of rows
- `WithSqlInputJson(commandText, json)` — injects raw JSON for `[SqlInput]`
- `[SqlOutput]` captured generically by `FunctionInvocationResult.OutputData` or `ReadReturnValueAs<T>()`
- Tested across 4-flavour matrix: `IHostBuilder`×gRPC, `IHostBuilder`×ASP.NET Core, `FunctionsApplicationBuilder`×gRPC, `FunctionsApplicationBuilder`×ASP.NET Core

**NuGet dependency:** `Microsoft.Azure.Functions.Worker.Extensions.Sql`

---

### Issue 10: Redis Triggers, Input & Output bindings

**Package:** `AzureFunctions.TestFramework.Redis`

**Bindings:**
- **Trigger:** `[RedisPubSubTrigger]` — fires on Redis pub/sub channel messages
- **Trigger:** `[RedisListTrigger]` — fires on Redis list push events (LPUSH/RPUSH)
- **Trigger:** `[RedisStreamTrigger]` — fires on Redis stream entries
- **Input:** `[RedisInput]` — reads a value from the Redis cache
- **Output:** `[RedisOutput]` — writes a value to the Redis cache

**Scope:**
- New package: `AzureFunctions.TestFramework.Redis`
- Extension methods per trigger type:
  - `InvokeRedisPubSubAsync(functionName, channel, message)` — pub/sub trigger
  - `InvokeRedisListAsync(functionName, key, value)` — list trigger
  - `InvokeRedisStreamAsync(functionName, key, entries)` — stream trigger
- `ISyntheticBindingProvider` (`RedisInputSyntheticBindingProvider`): `WithRedisInput(key, value)` — injects fake cached value for `[RedisInput]`
- Output bindings captured generically by `FunctionInvocationResult.OutputData`
- Test across 4-flavour matrix

**NuGet dependency:** `Microsoft.Azure.Functions.Worker.Extensions.Redis`

---

### Issue 11: Azure Data Explorer Input & Output bindings *(preview)*

**Package:** `AzureFunctions.TestFramework.DataExplorer`

**Bindings:**
- **Input:** `[KustoInput]` — reads query results from an Azure Data Explorer (Kusto) cluster
- **Output:** `[KustoOutput]` — ingests rows into an Azure Data Explorer table

> **Note:** No trigger binding exists for Azure Data Explorer. This is an input/output-only extension (preview).

**Scope:**
- New package: `AzureFunctions.TestFramework.DataExplorer`
- `ISyntheticBindingProvider` (`KustoInputSyntheticBindingProvider`): `WithKustoInputRows(database, table, rows)` — injects fake query results for `[KustoInput]`
- Output bindings captured generically by `FunctionInvocationResult.OutputData`
- Test across 4-flavour matrix

**NuGet dependency:** `Microsoft.Azure.Functions.Worker.Extensions.Kusto` *(preview)*

---

### ~~Issue 12: MCP (Model Context Protocol) Trigger~~ ✅ Done

**Package:** `AzureFunctions.TestFramework.Mcp` — shipped.

See the "Already Supported" section above for the full binding audit. Key facts:
- `InvokeMcpToolAsync(functionName, toolArguments?, toolName?, sessionId?)` — invokes an MCP tool trigger with optional named arguments, custom tool name, and session ID
- `InvokeMcpResourceAsync(functionName, resourceUri, sessionId?)` — invokes an MCP resource trigger with a resource URI
- `InvokeMcpPromptAsync(functionName, arguments?, promptName?, sessionId?)` — invokes an MCP prompt trigger with optional arguments and custom prompt name
- MCP triggers require extension middleware (`FunctionsMcpContextMiddleware`) to populate `FunctionContext.Items`; the framework invokes the `WorkerExtensionStartupCodeExecutor` from the functions assembly automatically (see `docs/Reflection.md` §§ 10–11)
- Tested across 4-flavour matrix: `IHostBuilder`×gRPC, `IHostBuilder`×ASP.NET Core, `FunctionsApplicationBuilder`×gRPC, `FunctionsApplicationBuilder`×ASP.NET Core

**NuGet dependency:** `Microsoft.Azure.Functions.Worker.Extensions.Mcp`

---

### Issue 13: Dapr Triggers, Input & Output bindings

**Package:** `AzureFunctions.TestFramework.Dapr`

**Bindings:**
- **Trigger:** `[DaprBindingTrigger]` — fires on a Dapr input binding event
- **Trigger:** `[DaprServiceInvocationTrigger]` — fires on a Dapr service invocation call
- **Trigger:** `[DaprTopicTrigger]` — fires on a Dapr pub/sub topic message
- **Input:** `[DaprStateInput]` — reads state from a Dapr state store
- **Input:** `[DaprSecretInput]` — reads a secret from a Dapr secret store
- **Output:** `[DaprStateOutput]` — saves state to a Dapr state store
- **Output:** `[DaprInvokeOutput]` — invokes another Dapr app
- **Output:** `[DaprPublishOutput]` — publishes a message to a Dapr topic
- **Output:** `[DaprBindingOutput]` — sends a value to a Dapr output binding

> **Note:** The Dapr extension is supported in Kubernetes, Azure Container Apps, Azure IoT Edge, and other self-hosted modes only. It is not available in the Azure Functions Consumption plan.

**Scope:**
- New package: `AzureFunctions.TestFramework.Dapr`
- Extension methods per trigger type:
  - `InvokeDaprBindingAsync(functionName, bindingName, operation, data)` — Dapr input binding trigger
  - `InvokeDaprServiceInvocationAsync(functionName, appId, methodName, body)` — service invocation trigger
  - `InvokeDaprTopicAsync(functionName, pubSubName, topic, data)` — pub/sub topic trigger
- `ISyntheticBindingProvider` implementations for state and secret input bindings
- Output bindings captured generically by `FunctionInvocationResult.OutputData`
- Test across 4-flavour matrix

**NuGet dependency:** `Microsoft.Azure.Functions.Worker.Extensions.Dapr`

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
4. ~~**Azure SQL** — High demand for data-driven functions; trigger + input + output~~ ✅ Done
5. **Redis** — Growing adoption for caching and event-driven patterns; three trigger variants
6. **Kafka** — Growing adoption
7. ~~**MCP** — New AI/agent integration pattern; trigger-only, relatively simple~~ ✅ Done
8. **RabbitMQ** — Niche but important
9. **SendGrid** — Output-only, low complexity
10. **Dapr** — Kubernetes/Container Apps only; rich binding set
11. **Azure Data Explorer** — Preview, input/output only; niche data-engineering scenarios
12. **Warmup** — Simplest, rarely tested in isolation
