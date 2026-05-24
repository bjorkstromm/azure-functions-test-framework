# AzureFunctions.TestFramework.Warmup

[![NuGet](https://img.shields.io/nuget/v/AzureFunctions.TestFramework.Warmup.svg)](https://www.nuget.org/packages/AzureFunctions.TestFramework.Warmup)

WarmupTrigger invocation support for the Azure Functions Test Framework. Provides `InvokeWarmupAsync(...)` — an extension on `IFunctionsTestHost` that lets you trigger warmup functions directly from integration tests.

## Usage

```csharp
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Warmup;

public class WarmupFunctionTests : IAsyncLifetime
{
    private IFunctionsTestHost _testHost;

    public async Task InitializeAsync()
    {
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(MyWarmupFunction).Assembly)
            .BuildAndStartAsync();
    }

    [Fact]
    public async Task WarmupTrigger_RunsSuccessfully()
    {
        var result = await _testHost.InvokeWarmupAsync("WarmupTrigger");
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
Task<FunctionInvocationResult> InvokeWarmupAsync(
    this IFunctionsTestHost host,
    string functionName,
    WarmupContext? context = null,
    CancellationToken cancellationToken = default)
```

- **`functionName`** — the name of the warmup function (case-insensitive).
- **`context`** — optional `WarmupContext` passed to the function.

## References

- [Full documentation](https://github.com/bjorkstromm/azure-functions-test-framework)
- [`AzureFunctions.TestFramework.Core`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Core)

## License

MIT
