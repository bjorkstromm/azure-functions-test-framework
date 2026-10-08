# AzureFunctions.TestFramework.ServiceBus

[![NuGet](https://img.shields.io/nuget/v/AzureFunctions.TestFramework.ServiceBus.svg)](https://www.nuget.org/packages/AzureFunctions.TestFramework.ServiceBus)

ServiceBusTrigger invocation support for the Azure Functions Test Framework. Provides `InvokeServiceBusAsync(...)`, `InvokeServiceBusBatchAsync(...)`, and `ConfigureFakeServiceBusMessageActions()` — extensions on `IFunctionsTestHost` that let you trigger Service Bus-triggered functions directly from integration tests without a real Azure Service Bus namespace.

## Usage

Pass a body directly as a string, raw `byte[]`, or `BinaryData`, or pass a POCO to serialize as JSON:

```csharp
await host.InvokeServiceBusAsync("ProcessMessage", "Hello!");
await host.InvokeServiceBusAsync("ProcessBytes", new byte[] { 0, 255, 128 });
await host.InvokeServiceBusAsync("ProcessBytes", BinaryData.FromBytes(new byte[] { 0, 255 }));
await host.InvokeServiceBusAsync("ProcessOrder", new { OrderId = "123", Quantity = 2 });
await host.InvokeServiceBusAsync("ProcessOrder", order,
    jsonSerializerOptions: new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower },
    cancellationToken: cancellationToken);
```

Strings are UTF-8 encoded without JSON quoting; byte arrays and `BinaryData` preserve raw bytes.
POCO payloads default to camel-case JSON property names. Custom options require `using System.Text.Json;`.
Use the existing `ServiceBusMessage` overload for body plus trigger metadata, or
`ServiceBusReceivedMessage` for SDK-typed function parameters and AMQP message metadata.
`InvokeServiceBusBatchAsync` always delivers an SDK message collection, including singleton batches;
null, empty, and null-containing batches are rejected.

```csharp
using Azure.Messaging.ServiceBus;
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.ServiceBus;

public class ServiceBusFunctionTests : IAsyncLifetime
{
    private IFunctionsTestHost _testHost;

    public async Task InitializeAsync()
    {
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(MyServiceBusFunction).Assembly)
            .ConfigureFakeServiceBusMessageActions()   // required when functions accept ServiceBusMessageActions
            .BuildAndStartAsync();
    }

    // ── Single message (string / byte[] function parameter) ───────────────────────────

    [Fact]
    public async Task ProcessMessage_WithStringBody_Succeeds()
    {
        var message = new ServiceBusMessage("Hello from test!");
        var result = await _testHost.InvokeServiceBusAsync("ProcessOrderMessage", message);
        Assert.True(result.Success);
    }

    // ── Single message (ServiceBusReceivedMessage function parameter) ─────────────────

    [Fact]
    public async Task ProcessMessage_WithReceivedMessage_Succeeds()
    {
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("Hello from test!"),
            messageId: Guid.NewGuid().ToString());

        var result = await _testHost.InvokeServiceBusAsync("ProcessOrderMessage", message);
        Assert.True(result.Success);
    }

    // ── Batch mode (IsBatched = true, ServiceBusReceivedMessage[] parameter) ──────────

    [Fact]
    public async Task ProcessBatch_WithMultipleMessages_Succeeds()
    {
        var messages = Enumerable.Range(1, 3)
            .Select(i => ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString($"order {i}"),
                messageId: Guid.NewGuid().ToString()))
            .ToList()
            .AsReadOnly();

        var result = await _testHost.InvokeServiceBusBatchAsync("ProcessOrderBatch", messages);
        Assert.True(result.Success);
    }

    // ── ServiceBusMessageActions injection ────────────────────────────────────────────

    [Fact]
    public async Task ProcessMessage_CompletesMessageViaActions()
    {
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("Hello!"),
            messageId: Guid.NewGuid().ToString());

        var result = await _testHost.InvokeServiceBusAsync("ProcessOrderMessage", message);
        Assert.True(result.Success);

        // Verify that the function called CompleteMessageAsync
        var actions = _testHost.Services.GetRequiredService<FakeServiceBusMessageActions>();
        Assert.Single(actions.RecordedActions);
        Assert.Equal("Complete", actions.RecordedActions[0].Action);
    }

    public async Task DisposeAsync()
    {
        await _testHost.StopAsync();
        _testHost.Dispose();
    }
}
```

## `ServiceBusMessageActions` and `ServiceBusSessionMessageActions`

Functions that accept `ServiceBusMessageActions` or `ServiceBusSessionMessageActions` as parameters need fake implementations because the real SDK converters require a live gRPC settlement channel. Call `ConfigureFakeServiceBusMessageActions()` on the builder to register the fakes:

```csharp
_testHost = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunction).Assembly)
    .ConfigureFakeServiceBusMessageActions()  // registers fakes + intercepts SDK converters
    .BuildAndStartAsync();
```

After the invocation, resolve the fake from `host.Services` to assert settlement calls:

```csharp
var actions = host.Services.GetRequiredService<FakeServiceBusMessageActions>();
// RecordedActions contains every Complete/Abandon/DeadLetter/Defer/RenewLock call made during the invocation
Assert.Equal("Complete", actions.RecordedActions[0].Action);

// Reset between tests if the host is shared (IClassFixture / OneTimeSetUp)
actions.Reset();
```

For session-enabled topics/queues:

```csharp
var sessionActions = host.Services.GetRequiredService<FakeServiceBusSessionMessageActions>();
Assert.Contains(sessionActions.RecordedActions, a => a.Action == "RenewSessionLock");
```

## References

- [Full documentation](https://github.com/bjorkstromm/azure-functions-test-framework)
- [`AzureFunctions.TestFramework.Core`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Core)

## License

MIT
