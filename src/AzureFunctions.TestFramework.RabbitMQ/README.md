# AzureFunctions.TestFramework.RabbitMQ

[![NuGet](https://img.shields.io/nuget/v/AzureFunctions.TestFramework.RabbitMQ.svg)](https://www.nuget.org/packages/AzureFunctions.TestFramework.RabbitMQ)

`RabbitMQTrigger` invocation support for the Azure Functions Test Framework. Provides `InvokeRabbitMQAsync(...)` — extensions on `IFunctionsTestHost` that let you trigger RabbitMQ-triggered functions directly from integration tests without a real RabbitMQ broker.

## Usage

### Functions with `string` parameter

```csharp
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.RabbitMQ;

[Fact]
public async Task ProcessRabbitMq_WithStringBody_Succeeds()
{
    var result = await _testHost.InvokeRabbitMQAsync("ProcessRabbitMqMessage", "Hello from test!");
    Assert.True(result.Success);
}
```

### Optional delivery / application properties

Pass a `RabbitMqTriggerMessageProperties` instance (for example `RoutingKey`, `MessageId`, `Exchange`, `Headers`) so values appear in `InvocationRequest.TriggerMetadata` and are available from `FunctionContext.BindingContext.BindingData` in the function:

```csharp
var props = new RabbitMqTriggerMessageProperties
{
    RoutingKey = "my.route",
    MessageId = "correlation-1"
};
var result = await _testHost.InvokeRabbitMQAsync(
    "ProcessRabbitMqWithMetadata",
    "payload",
    props,
    TestContext.Current.CancellationToken);
Assert.True(result.Success);
```

### Functions with `byte[]` or `BinaryData` parameter

Use the `byte[]` overload and pass the raw body bytes. The same optional `RabbitMqTriggerMessageProperties` overload exists after the `byte[]` argument.

### Functions with a JSON POCO parameter

Use the generic overload when the trigger parameter is a type deserialized from JSON (dotnet-isolated):

```csharp
var result = await _testHost.InvokeRabbitMQAsync(
    "ProcessRabbitMqOrder",
    new RabbitMqOrderPayload { OrderId = "order-42" });
Assert.True(result.Success);
```

Optional metadata can be passed between `payload` and `JsonSerializerOptions`.

### API

```csharp
Task<FunctionInvocationResult> InvokeRabbitMQAsync(
    this IFunctionsTestHost host,
    string functionName,
    string message,
    CancellationToken cancellationToken = default)

Task<FunctionInvocationResult> InvokeRabbitMQAsync(
    this IFunctionsTestHost host,
    string functionName,
    string message,
    RabbitMqTriggerMessageProperties? messageProperties,
    CancellationToken cancellationToken = default)

Task<FunctionInvocationResult> InvokeRabbitMQAsync(
    this IFunctionsTestHost host,
    string functionName,
    byte[] body,
    CancellationToken cancellationToken = default)

Task<FunctionInvocationResult> InvokeRabbitMQAsync(
    this IFunctionsTestHost host,
    string functionName,
    byte[] body,
    RabbitMqTriggerMessageProperties? messageProperties,
    CancellationToken cancellationToken = default)

Task<FunctionInvocationResult> InvokeRabbitMQAsync<T>(
    this IFunctionsTestHost host,
    string functionName,
    T payload,
    JsonSerializerOptions? jsonSerializerOptions = null,
    CancellationToken cancellationToken = default)

Task<FunctionInvocationResult> InvokeRabbitMQAsync<T>(
    this IFunctionsTestHost host,
    string functionName,
    T payload,
    RabbitMqTriggerMessageProperties? messageProperties,
    JsonSerializerOptions? jsonSerializerOptions = null,
    CancellationToken cancellationToken = default)
```

- **`functionName`** — the name of the RabbitMQ function (case-insensitive).
- **`message`** / **`body`** — payload delivered to the trigger binding (UTF-8 text or raw bytes).
- **`payload`** — object serialized to JSON for POCO trigger parameters.
- **`messageProperties`** — optional metadata mapped to gRPC trigger metadata (RabbitMQ / AMQP-style keys).

### Output binding capture

Output bindings (for example `[RabbitMQOutput]` on a return type property) are captured in `FunctionInvocationResult.OutputData` — use `ReadOutputAs<T>(bindingName)` (typically the **property name** on your return POCO). The raw `ReturnValue` is also populated for diagnostics; prefer `ReadOutputAs` for asserting named output bindings.

## References

- [Full documentation](https://github.com/bjorkstromm/azure-functions-test-framework)
- [`AzureFunctions.TestFramework.Core`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Core)
- [Azure RabbitMQ bindings for Azure Functions](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-rabbitmq)

## License

MIT
