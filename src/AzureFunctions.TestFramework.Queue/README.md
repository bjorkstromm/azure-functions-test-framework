# AzureFunctions.TestFramework.Queue

[![NuGet](https://img.shields.io/nuget/v/AzureFunctions.TestFramework.Queue.svg)](https://www.nuget.org/packages/AzureFunctions.TestFramework.Queue)

QueueTrigger invocation support for the Azure Functions Test Framework. Provides `InvokeQueueAsync(...)` — an extension on `IFunctionsTestHost` that lets you trigger queue-triggered functions directly from integration tests without a real Azure Storage queue.

## Usage

```csharp
using Azure.Storage.Queues.Models;
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Queue;

public class QueueFunctionTests : IAsyncLifetime
{
    private IFunctionsTestHost _testHost;

    public async Task InitializeAsync()
    {
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(MyQueueFunction).Assembly)
            .BuildAndStartAsync();
    }

    [Fact]
    public async Task ProcessQueueMessage_WithTextMessage_Succeeds()
    {
        var message = QueuesModelFactory.QueueMessage(
            messageId: Guid.NewGuid().ToString(),
            popReceipt: "pop-receipt",
            messageText: "Hello from queue!",
            dequeueCount: 1);

        var result = await _testHost.InvokeQueueAsync("ProcessQueueMessage", message);
        Assert.True(result.Success);
    }

    public async Task DisposeAsync()
    {
        await _testHost.StopAsync();
        _testHost.Dispose();
    }
}
```

### API

```csharp
Task<FunctionInvocationResult> InvokeQueueAsync(
    this IFunctionsTestHost host,
    string functionName,
    QueueMessage message,
    CancellationToken cancellationToken = default)
```

- **`functionName`** — the name of the queue function (case-insensitive).
- **`message`** — the `QueueMessage` to pass to the function. Use `QueuesModelFactory.QueueMessage(...)` from `Azure.Storage.Queues.Models` to create test messages.

### Output binding capture

Use `FunctionInvocationResult` to inspect output bindings produced by the function:

```csharp
var message = QueuesModelFactory.QueueMessage(
    Guid.NewGuid().ToString(), "pop-receipt", "order-123", 1);

var result = await _testHost.InvokeQueueAsync("ProcessOrder", message);
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
