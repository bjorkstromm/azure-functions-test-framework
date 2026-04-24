# AzureFunctions.TestFramework.Dapr

Dapr Trigger, Input, and Output binding support for the [Azure Functions Test Framework](../../README.md).

## Installation

```bash
dotnet add package AzureFunctions.TestFramework.Dapr
```

## Supported bindings

| Binding | Attribute | Description |
|---------|-----------|-------------|
| `[DaprBindingTrigger]` | Trigger | Fires on a Dapr input binding event |
| `[DaprServiceInvocationTrigger]` | Trigger | Fires on a Dapr service invocation call |
| `[DaprTopicTrigger]` | Trigger | Fires on a Dapr pub/sub topic message |
| `[DaprStateInput]` | Input | Reads state from a Dapr state store (see [limitations](#dapr-input-binding-limitations)) |
| `[DaprSecretInput]` | Input | Reads a secret from a Dapr secret store (see [limitations](#dapr-input-binding-limitations)) |
| `[DaprStateOutput]` | Output | Saves state to a Dapr state store — captured via `FunctionInvocationResult` |
| `[DaprInvokeOutput]` | Output | Invokes another Dapr app — captured via `FunctionInvocationResult` |
| `[DaprPublishOutput]` | Output | Publishes a message to a Dapr topic — captured via `FunctionInvocationResult` |
| `[DaprBindingOutput]` | Output | Sends a value to a Dapr output binding — captured via `FunctionInvocationResult` |

> **Note:** The Dapr extension is supported in Kubernetes, Azure Container Apps, Azure IoT Edge, and other self-hosted modes only. It is not available in the Azure Functions Consumption plan.

## DaprBindingTrigger

Use `InvokeDaprBindingAsync` to simulate a Dapr input binding event trigger.

```csharp
var result = await host.InvokeDaprBindingAsync(
    "ProcessDaprBinding",
    data: "my-event-data");

Assert.True(result.Success);
```

Pass a POCO to have it serialized to JSON automatically:

```csharp
var payload = new MyEvent { Id = "evt-1", Message = "hello" };

var result = await host.InvokeDaprBindingAsync(
    "ProcessDaprBinding",
    data: payload);

Assert.True(result.Success);
```

### Function example

```csharp
[Function("ProcessDaprBinding")]
public void Run(
    [DaprBindingTrigger(BindingName = "my-binding")] string data)
{
    _logger.LogInformation("Received Dapr binding event: {Data}", data);
}
```

## DaprServiceInvocationTrigger

Use `InvokeDaprServiceInvocationAsync` to simulate a Dapr service invocation call.

```csharp
var result = await host.InvokeDaprServiceInvocationAsync(
    "HandleInvocation",
    body: "request-body");

Assert.True(result.Success);
```

Pass a POCO to have it serialized to JSON automatically:

```csharp
var request = new MyRequest { OrderId = "ord-42" };

var result = await host.InvokeDaprServiceInvocationAsync(
    "HandleInvocation",
    body: request);

Assert.True(result.Success);
```

### Function example

```csharp
[Function("HandleInvocation")]
public void Run(
    [DaprServiceInvocationTrigger] string body)
{
    _logger.LogInformation("Dapr service invocation: {Body}", body);
}
```

## DaprTopicTrigger

Use `InvokeDaprTopicAsync` to simulate a Dapr pub/sub topic message trigger.

```csharp
var result = await host.InvokeDaprTopicAsync(
    "ProcessTopicMessage",
    message: "hello from dapr pub/sub");

Assert.True(result.Success);
```

Pass a POCO to have it serialized to JSON automatically:

```csharp
var order = new Order { Id = "ord-1", Amount = 99.99m };

var result = await host.InvokeDaprTopicAsync(
    "ProcessTopicMessage",
    message: order);

Assert.True(result.Success);
```

### Function example

```csharp
[Function("ProcessTopicMessage")]
public void Run(
    [DaprTopicTrigger("my-pubsub", Topic = "orders")] string message)
{
    _logger.LogInformation("Received Dapr topic message: {Message}", message);
}
```

## Output bindings

Output bindings are captured automatically via `FunctionInvocationResult` when the worker SDK correctly marks them as output bindings.

> **Note:** Due to a known bug in the Dapr extension's source generator (v1.0.1), properties decorated
> with Dapr output binding attributes (e.g. `[DaprPublishOutput]`, `[DaprStateOutput]`, etc.) on POCO
> return types are generated with `direction: "In"` instead of `direction: "Out"`. As a result, the
> worker SDK does not populate `InvocationResponse.OutputData` for these properties and
> `FunctionInvocationResult.OutputData` will be empty. Functions with Dapr output bindings still
> execute successfully.

## Dapr input binding limitations

> **Note:** The Azure Functions Worker SDK source generator (as of v2.0.7) does not emit binding
> metadata for `[DaprStateInput]` or `[DaprSecretInput]` parameters. Because of this, the
> `WithDaprStateInput` and `WithDaprSecretInput` builder extensions have no effect in
> source-generated mode.

To work with `[DaprStateInput]` or `[DaprSecretInput]` in integration tests, override the Dapr
HTTP client in DI to return a fake response from the Dapr sidecar endpoint:

```csharp
var host = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunction).Assembly)
    .WithHostBuilderFactory(Program.CreateHostBuilder)
    .ConfigureServices(services =>
    {
        // Replace the Dapr HTTP client to return a fake state value
        services.AddHttpClient("dapr-client", client => { ... });
    })
    .BuildAndStartAsync();
```

When the source generator is updated to emit `daprState` and `daprSecret` binding metadata, the
`WithDaprStateInput` and `WithDaprSecretInput` extensions will work automatically without any
additional configuration.

## Testing across all four flavours

Add the Dapr package reference to your test project and all four function-app test flavours:

```xml
<PackageReference Include="AzureFunctions.TestFramework.Dapr" />
```

See the [4-flavour matrix test pattern](../../tests/) for the concrete test class structure.
