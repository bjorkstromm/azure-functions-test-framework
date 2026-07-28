# AzureFunctions.TestFramework.Queue

[![NuGet](https://img.shields.io/nuget/v/AzureFunctions.TestFramework.Queue.svg)](https://www.nuget.org/packages/AzureFunctions.TestFramework.Queue)

QueueTrigger invocation support for the Azure Functions Test Framework. Provides `InvokeQueueAsync(...)` — an extension on `IFunctionsTestHost` that lets you trigger queue-triggered functions directly from integration tests without a real Azure Storage queue.

## Usage

### Functions with `string` parameter

Use the `string` overload when the function parameter is `string`:

```csharp
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Queue;

[Fact]
public async Task ProcessQueueMessage_WithTextMessage_Succeeds()
{
    var result = await _testHost.InvokeQueueAsync("ProcessQueueMessage", "Hello from queue!");
    Assert.True(result.Success);
}
```

### Functions with `QueueMessage` parameter

Use the `QueueMessage` overload when the function parameter is typed as `QueueMessage`:

```csharp
using Azure.Storage.Queues.Models;
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Queue;

[Fact]
public async Task ProcessQueueMessage_WithQueueMessage_Succeeds()
{
    var message = QueuesModelFactory.QueueMessage(
        messageId: Guid.NewGuid().ToString(),
        popReceipt: "pop-receipt",
        messageText: "Hello from queue!",
        dequeueCount: 1);

    var result = await _testHost.InvokeQueueAsync("ProcessQueueMessage", message);
    Assert.True(result.Success);
}
```

### Functions with `byte[]` or `BinaryData` parameter

Use the `byte[]` overload for raw queue payloads:

```csharp
var bytes = Encoding.UTF8.GetBytes("Hello from queue!");
var result = await _testHost.InvokeQueueAsync("ProcessQueueMessageBytes", bytes);
Assert.True(result.Success);
```

### Functions with POCO parameter

Use the generic overload to send JSON payloads for POCO input bindings:

```csharp
var payload = new OrderPayload { OrderId = "order-99" };
var result = await _testHost.InvokeQueueAsync("ProcessQueueMessagePoco", payload);
Assert.True(result.Success);
```

### API

```csharp
// For functions with string parameters
Task<FunctionInvocationResult> InvokeQueueAsync(
    this IFunctionsTestHost host,
    string functionName,
    string message,
    CancellationToken cancellationToken = default)

// For functions with byte[]/BinaryData parameters
Task<FunctionInvocationResult> InvokeQueueAsync(
    this IFunctionsTestHost host,
    string functionName,
    byte[] message,
    CancellationToken cancellationToken = default)

// For functions with POCO parameters (JSON payload)
Task<FunctionInvocationResult> InvokeQueueAsync<T>(
    this IFunctionsTestHost host,
    string functionName,
    T payload,
    JsonSerializerOptions? jsonSerializerOptions = null,
    CancellationToken cancellationToken = default)

// For functions with QueueMessage parameters
Task<FunctionInvocationResult> InvokeQueueAsync(
    this IFunctionsTestHost host,
    string functionName,
    QueueMessage message,
    CancellationToken cancellationToken = default)
```

- **`functionName`** — the name of the queue function (case-insensitive).
- **`message`** (`string`) — the UTF-8 message text to pass to the function.
- **`message`** (`byte[]`) — raw message bytes to pass to the function.
- **`payload`** (`T`) — POCO payload serialized as JSON for queue input binding.
- **`message`** (`QueueMessage`) — the `QueueMessage` to pass to the function. Use `QueuesModelFactory.QueueMessage(...)` from `Azure.Storage.Queues.Models` to create test messages.

### Output binding capture

Use `FunctionInvocationResult` to inspect output bindings produced by the function:

```csharp
var result = await _testHost.InvokeQueueAsync("ProcessOrder", "order-123");
Assert.True(result.Success);

// Read a named output binding (e.g. [QueueOutput("results")])
var outputMessage = result.ReadOutputAs<string>("OutputMessage");
Assert.Equal("processed: order-123", outputMessage);

// Read the function return value
var returnValue = result.ReadReturnValueAs<string>();
```

## References

- [Full documentation](https://github.com/bjorkstromm/azure-functions-test-framework)
- [`AzureFunctions.TestFramework.Core`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Core)

## License

MIT
