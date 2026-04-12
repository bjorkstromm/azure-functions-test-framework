# AzureFunctions.TestFramework.EventHubs

[![NuGet](https://img.shields.io/nuget/v/AzureFunctions.TestFramework.EventHubs.svg)](https://www.nuget.org/packages/AzureFunctions.TestFramework.EventHubs)

EventHubTrigger invocation support for the Azure Functions Test Framework. Provides `InvokeEventHubAsync(...)` and `InvokeEventHubBatchAsync(...)` — extensions on `IFunctionsTestHost` that let you trigger Event Hubs-triggered functions directly from integration tests without a real Azure Event Hubs namespace.

## Usage

```csharp
using Azure.Messaging.EventHubs;
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.EventHubs;

public class EventHubFunctionTests : IAsyncLifetime
{
    private IFunctionsTestHost _testHost;

    public async Task InitializeAsync()
    {
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(MyEventHubFunction).Assembly)
            .BuildAndStartAsync();
    }

    // Single event (IsBatched = false)
    [Fact]
    public async Task ProcessEvent_WithSingleEventData_Succeeds()
    {
        var eventData = new EventData(BinaryData.FromString("Hello from Event Hubs!"));

        var result = await _testHost.InvokeEventHubAsync("ProcessEventHubMessage", eventData);
        Assert.True(result.Success);
    }

    // Batch events (default IsBatched = true)
    [Fact]
    public async Task ProcessEvents_WithBatch_Succeeds()
    {
        var events = new[]
        {
            new EventData(BinaryData.FromString("Event 1")),
            new EventData(BinaryData.FromString("Event 2")),
        };

        var result = await _testHost.InvokeEventHubBatchAsync("ProcessEventHubBatch", events);
        Assert.True(result.Success);
    }

    // Output binding capture
    [Fact]
    public async Task ProcessEvent_WithOutputBinding_CapturesOutput()
    {
        var eventData = new EventData(BinaryData.FromString("trigger"));

        var result = await _testHost.InvokeEventHubAsync("ProcessAndForward", eventData);

        Assert.True(result.Success);
        var output = result.ReadReturnValueAs<string>();
        Assert.NotNull(output);
    }

    public async Task DisposeAsync() => await _testHost.DisposeAsync();
}
```

### API

```csharp
// Single event (use when IsBatched = false)
Task<FunctionInvocationResult> InvokeEventHubAsync(
    this IFunctionsTestHost host,
    string functionName,
    EventData eventData,
    CancellationToken cancellationToken = default)

// Batch events (use when IsBatched = true, the default)
Task<FunctionInvocationResult> InvokeEventHubBatchAsync(
    this IFunctionsTestHost host,
    string functionName,
    IReadOnlyList<EventData> events,
    CancellationToken cancellationToken = default)
```

- **`functionName`** — the name of the Event Hubs function (case-insensitive).
- **`eventData`** — the single `EventData` to pass to the function.
- **`events`** — the batch of `EventData` instances to pass to the function.

### Output Bindings

`[EventHubOutput]` output bindings are captured generically by `FunctionInvocationResult.OutputData` — no extra configuration is needed:

```csharp
var result = await _testHost.InvokeEventHubAsync("MyFunction", eventData);
var output = result.ReadOutputAs<string>("outputEventHub");  // named output binding
// OR for return value output:
var output = result.ReadReturnValueAs<string>();
```

## Notes

- **Batch mode is the default** for `[EventHubTrigger]` (`IsBatched = true`). Use `InvokeEventHubBatchAsync` for batch-mode functions and `InvokeEventHubAsync` for single-event functions (`IsBatched = false`).
- Both single and batch overloads encode `EventData` as AMQP messages using the same wire format as the Azure Functions Event Hubs extension.
- `EventData` properties such as `MessageId`, `CorrelationId`, and custom properties set via `Properties` are preserved through AMQP encoding.

## References

- [Azure Functions Event Hubs trigger](https://docs.microsoft.com/azure/azure-functions/functions-bindings-event-hubs-trigger)
- [Azure Functions Event Hubs output](https://docs.microsoft.com/azure/azure-functions/functions-bindings-event-hubs-output)
- [Azure.Messaging.EventHubs](https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/eventhub/Azure.Messaging.EventHubs)

## License

MIT
