# AzureFunctions.TestFramework.EventGrid

[![NuGet](https://img.shields.io/nuget/v/AzureFunctions.TestFramework.EventGrid.svg)](https://www.nuget.org/packages/AzureFunctions.TestFramework.EventGrid)

EventGridTrigger invocation support for the Azure Functions Test Framework. Provides `InvokeEventGridAsync(...)` — an extension on `IFunctionsTestHost` that lets you trigger Event Grid-triggered functions directly from integration tests. Both `EventGridEvent` (EventGrid schema) and `CloudEvent` (CloudEvents schema) are supported.

## Usage

```csharp
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.EventGrid;

public class EventGridFunctionTests : IAsyncLifetime
{
    private IFunctionsTestHost _testHost;

    public async Task InitializeAsync()
    {
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(MyEventGridFunction).Assembly)
            .BuildAndStartAsync();
    }

    [Fact]
    public async Task ProcessEvent_WithEventGridEvent_Succeeds()
    {
        var eventGridEvent = new EventGridEvent(
            subject: "orders/order-123",
            eventType: "Order.Created",
            dataVersion: "1.0",
            data: BinaryData.FromObjectAsJson(new { orderId = "123" }));

        var result = await _testHost.InvokeEventGridAsync("ProcessOrderEvent", eventGridEvent);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task ProcessEvent_WithCloudEvent_Succeeds()
    {
        var cloudEvent = new CloudEvent(
            source: "/orders",
            type: "order.created",
            jsonSerializableData: new { orderId = "123" });

        var result = await _testHost.InvokeEventGridAsync("ProcessOrderEvent", cloudEvent);
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
// EventGrid schema
Task<FunctionInvocationResult> InvokeEventGridAsync(
    this IFunctionsTestHost host,
    string functionName,
    EventGridEvent eventGridEvent,
    CancellationToken cancellationToken = default)

// CloudEvents schema
Task<FunctionInvocationResult> InvokeEventGridAsync(
    this IFunctionsTestHost host,
    string functionName,
    CloudEvent cloudEvent,
    CancellationToken cancellationToken = default)
```

- **`functionName`** — the name of the Event Grid function (case-insensitive).
- **`eventGridEvent`** / **`cloudEvent`** — the event to pass to the function.

## References

- [Full documentation](https://github.com/bjorkstromm/azure-functions-test-framework)
- [`AzureFunctions.TestFramework.Core`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Core)

## License

MIT
