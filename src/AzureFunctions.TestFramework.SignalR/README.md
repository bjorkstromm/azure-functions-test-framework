# AzureFunctions.TestFramework.SignalR

SignalRTrigger invocation and SignalR input/output binding support for the [Azure Functions Test Framework](https://github.com/bjorkstromm/azure-functions-test-framework).

## Overview

This package provides:

- **`InvokeSignalRAsync`** — fires a `[SignalRTrigger]` invocation against the in-process worker
- **`WithSignalRConnectionInfo`** — injects synthetic `[SignalRConnectionInfoInput]` binding data (no real SignalR service needed)
- **`WithSignalRNegotiation`** — injects synthetic `[SignalRNegotiationInput]` binding data
- **`WithSignalREndpoints`** — injects synthetic `[SignalREndpointsInput]` binding data
- **Output binding capture** — `[SignalROutput]` return values captured via `FunctionInvocationResult`

## Installation

```bash
dotnet add package AzureFunctions.TestFramework.SignalR
```

## Usage

### SignalR Trigger

```csharp
var invocationContext = new SignalRInvocationContext
{
    ConnectionId = "conn-123",
    UserId = "user-1",
    Hub = "chat",
    Category = SignalRInvocationCategory.Messages,
    Event = "sendMessage",
    Arguments = new object[] { "Hello World" }
};

var result = await host.InvokeSignalRAsync("ProcessSignalRMessage", invocationContext);

Assert.True(result.Success);
```

### SignalR Output Binding

```csharp
var result = await host.InvokeSignalRAsync("BroadcastSignalRMessage", invocationContext);

Assert.True(result.Success);
var action = result.ReadReturnValueAs<SignalRMessageAction>();
Assert.Equal("broadcast", action.Target);
```

### SignalRConnectionInfo Input Binding

Register synthetic connection info before the test host starts:

```csharp
var host = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunctions).Assembly)
    .WithHostBuilderFactory(Program.CreateWorkerHostBuilder)
    .WithSignalRConnectionInfo(
        url: "https://test.signalr.net/client/?hub=chat",
        accessToken: "test-access-token")
    .BuildAndStartAsync();

// The [SignalRConnectionInfoInput] parameter is automatically populated
var client = host.CreateHttpClient();
var response = await client.PostAsync("/api/negotiate", null);
```

### SignalRNegotiation Input Binding

```csharp
var negotiationContext = new SignalRNegotiationContext
{
    Endpoints =
    [
        new SignalREndpointConnectionInfo
        {
            EndpointType = SignalREndpointType.Primary,
            Name = "primary",
            Endpoint = "https://test.signalr.net",
            Online = true,
            ConnectionInfo = new SignalRConnectionInfo
            {
                Url = "https://test.signalr.net/client/?hub=chat",
                AccessToken = "test-token"
            }
        }
    ]
};

var host = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunctions).Assembly)
    .WithHostBuilderFactory(Program.CreateWorkerHostBuilder)
    .WithSignalRNegotiation(negotiationContext)
    .BuildAndStartAsync();
```

### SignalREndpoints Input Binding

```csharp
var endpoints = new[]
{
    new SignalREndpoint
    {
        EndpointType = SignalREndpointType.Primary,
        Name = "primary",
        Endpoint = "https://test.signalr.net",
        Online = true
    }
};

var host = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunctions).Assembly)
    .WithHostBuilderFactory(Program.CreateWorkerHostBuilder)
    .WithSignalREndpoints(endpoints)
    .BuildAndStartAsync();
```
