# AzureFunctions.TestFramework.Kafka

[![NuGet](https://img.shields.io/nuget/v/AzureFunctions.TestFramework.Kafka.svg)](https://www.nuget.org/packages/AzureFunctions.TestFramework.Kafka)

`KafkaTrigger` invocation support for the Azure Functions Test Framework. Provides `InvokeKafkaAsync(...)` and `InvokeKafkaBatchAsync(...)` — extension methods on `IFunctionsTestHost` that let you trigger Kafka-triggered functions directly from integration tests without a real Kafka broker.

## Usage

### Functions with `string` parameter

```csharp
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Kafka;

[Fact]
public async Task ProcessKafka_WithStringMessage_Succeeds()
{
    var result = await _testHost.InvokeKafkaAsync("ProcessKafkaMessage", "Hello from test!");
    Assert.True(result.Success);
}
```

The string is passed directly as the binding value; the function receives the text as-is.

### Functions with `byte[]` or `BinaryData` parameter

Use the `byte[]` overload to pass raw message body bytes:

```csharp
var body = System.Text.Encoding.UTF8.GetBytes("binary payload");
var result = await _testHost.InvokeKafkaAsync("ProcessKafkaBinary", body);
Assert.True(result.Success);
```

### Functions with a `KafkaRecord` parameter

Use the `KafkaRecord` overload when the function parameter is typed as `KafkaRecord`. The record is proto3-encoded and delivered as `ModelBindingData` using the Kafka extension's internal wire format:

```csharp
using Microsoft.Azure.Functions.Worker;

var record = new KafkaRecord
{
    Topic = "my-topic",
    Partition = 0,
    Offset = 42,
    Value = System.Text.Encoding.UTF8.GetBytes("event payload"),
    Key = System.Text.Encoding.UTF8.GetBytes("message-key"),
    Timestamp = new KafkaTimestamp { UnixTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
    Headers =
    [
        new KafkaHeader { Key = "correlation-id", Value = System.Text.Encoding.UTF8.GetBytes("abc-123") }
    ]
};

var result = await _testHost.InvokeKafkaAsync("ProcessKafkaRecord", record);
Assert.True(result.Success);
```

### Functions with a JSON POCO parameter

Use the generic overload when the trigger parameter is a type deserialized from JSON:

```csharp
var result = await _testHost.InvokeKafkaAsync(
    "ProcessKafkaOrder",
    new KafkaOrderPayload { OrderId = "order-42" });
Assert.True(result.Success);
```

### Batch trigger functions (`IsBatched = true`)

Use the `InvokeKafkaBatchAsync(...)` overloads for functions that use batched Kafka triggers:

```csharp
// string[] parameter
var result = await _testHost.InvokeKafkaBatchAsync(
    "ProcessKafkaBatch",
    ["event-1", "event-2", "event-3"]);
Assert.True(result.Success);

// KafkaRecord[] parameter
var records = new[] {
    new KafkaRecord { Topic = "orders", Value = Encoding.UTF8.GetBytes("order-1") },
    new KafkaRecord { Topic = "orders", Value = Encoding.UTF8.GetBytes("order-2") }
};
var result = await _testHost.InvokeKafkaBatchAsync("ProcessKafkaRecordBatch", records);
Assert.True(result.Success);
```

## API

```csharp
// Single string message
Task<FunctionInvocationResult> InvokeKafkaAsync(
    this IFunctionsTestHost host,
    string functionName,
    string message,
    CancellationToken cancellationToken = default)

// Batched string messages (IsBatched = true, string[] parameter)
Task<FunctionInvocationResult> InvokeKafkaBatchAsync(
    this IFunctionsTestHost host,
    string functionName,
    IReadOnlyList<string> messages,
    CancellationToken cancellationToken = default)

// Single byte[] message
Task<FunctionInvocationResult> InvokeKafkaAsync(
    this IFunctionsTestHost host,
    string functionName,
    byte[] body,
    CancellationToken cancellationToken = default)

// Batched byte[] messages
Task<FunctionInvocationResult> InvokeKafkaBatchAsync(
    this IFunctionsTestHost host,
    string functionName,
    IReadOnlyList<byte[]> bodies,
    CancellationToken cancellationToken = default)

// Single KafkaRecord (proto3-encoded ModelBindingData)
Task<FunctionInvocationResult> InvokeKafkaAsync(
    this IFunctionsTestHost host,
    string functionName,
    KafkaRecord record,
    CancellationToken cancellationToken = default)

// Batched KafkaRecord[] (proto3-encoded CollectionModelBindingData)
Task<FunctionInvocationResult> InvokeKafkaBatchAsync(
    this IFunctionsTestHost host,
    string functionName,
    IReadOnlyList<KafkaRecord> records,
    CancellationToken cancellationToken = default)

// Single JSON POCO
Task<FunctionInvocationResult> InvokeKafkaAsync<T>(
    this IFunctionsTestHost host,
    string functionName,
    T payload,
    JsonSerializerOptions? jsonSerializerOptions = null,
    CancellationToken cancellationToken = default)

// Batched JSON POCOs
Task<FunctionInvocationResult> InvokeKafkaBatchAsync<T>(
    this IFunctionsTestHost host,
    string functionName,
    IReadOnlyList<T> payloads,
    JsonSerializerOptions? jsonSerializerOptions = null,
    CancellationToken cancellationToken = default)
```

- **`functionName`** — the name of the Kafka function (case-insensitive).
- **`message`** / **`body`** — payload delivered to the trigger binding (text or raw bytes).
- **`record`** — a `KafkaRecord` instance that is proto3-encoded and passed as `ModelBindingData`.
- **`payload`** — object serialized to JSON for POCO trigger parameters.

## KafkaRecord binding

The `KafkaRecord` overload uses a custom proto3 binary encoder that reproduces the internal wire format of the Kafka extension's `KafkaRecordProto`. This ensures the extension's `KafkaRecordConverter` correctly deserializes the record in the isolated worker:

| KafkaRecord property | Proto field | Notes |
|----------------------|-------------|-------|
| `Topic`              | 1 (string)  | |
| `Partition`          | 2 (int32)   | |
| `Offset`             | 3 (int64)   | |
| `Key`                | 4 (bytes)   | Optional |
| `Value`              | 5 (bytes)   | Optional |
| `Timestamp`          | 6 (message) | `UnixTimestampMs` + `Type` |
| `Headers`            | 7 (repeated)| Each header: `Key` (string) + `Value` (bytes) |
| `LeaderEpoch`        | 8 (int32)   | Optional |

## Output binding capture

Output bindings (for example `[KafkaOutput]` on a return type property) are captured in `FunctionInvocationResult.OutputData` — use `ReadOutputAs<T>(bindingName)` (typically the **property name** on your return POCO). The raw `ReturnValue` is also populated for diagnostics; prefer `ReadOutputAs` for asserting named output bindings.

## References

- [Full documentation](https://github.com/bjorkstromm/azure-functions-test-framework)
- [`AzureFunctions.TestFramework.Core`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Core)
- [Azure Kafka bindings for Azure Functions](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-kafka)

## License

MIT
