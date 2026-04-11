# AzureFunctions.TestFramework.Timer

[![NuGet](https://img.shields.io/nuget/v/AzureFunctions.TestFramework.Timer.svg)](https://www.nuget.org/packages/AzureFunctions.TestFramework.Timer)

TimerTrigger invocation support for the Azure Functions Test Framework. Provides `InvokeTimerAsync(...)` — an extension on `IFunctionsTestHost` that lets you trigger timer-triggered functions directly from integration tests without scheduling or waiting.

## Usage

```csharp
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Timer;
using Microsoft.Azure.Functions.Worker;

public class TimerFunctionTests : IAsyncLifetime
{
    private IFunctionsTestHost _testHost;

    public async Task InitializeAsync()
    {
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(MyTimerFunction).Assembly)
            .BuildAndStartAsync();
    }

    [Fact]
    public async Task HeartbeatTimer_RunsSuccessfully()
    {
        // Invoke with default TimerInfo (IsPastDue = false)
        var result = await _testHost.InvokeTimerAsync("HeartbeatTimer");
        Assert.True(result.Success);
    }

    [Fact]
    public async Task HeartbeatTimer_WhenPastDue_RunsSuccessfully()
    {
        var timerInfo = new TimerInfo { IsPastDue = true };
        var result = await _testHost.InvokeTimerAsync("HeartbeatTimer", timerInfo);
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
Task<FunctionInvocationResult> InvokeTimerAsync(
    this IFunctionsTestHost host,
    string functionName,
    TimerInfo? timerInfo = null,
    CancellationToken cancellationToken = default)
```

- **`functionName`** — the name of the timer function (case-insensitive).
- **`timerInfo`** — optional `TimerInfo` passed to the function. When `null`, a default `TimerInfo` with `IsPastDue = false` and no schedule status is used.

### Output binding capture

Use `FunctionInvocationResult` to inspect output bindings produced by the function:

```csharp
var result = await _testHost.InvokeTimerAsync("HeartbeatTimer");
Assert.True(result.Success);

// Read a named output binding
var message = result.ReadOutputAs<string>("OutputMessage");
Assert.Equal("heartbeat", message);
```

## References

- [Full documentation](https://github.com/bjorkstromm/azure-functions-test-framework)
- [`AzureFunctions.TestFramework.Core`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Core)

## License

MIT
