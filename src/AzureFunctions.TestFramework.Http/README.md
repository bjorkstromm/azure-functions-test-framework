# AzureFunctions.TestFramework.Http

[![NuGet](https://img.shields.io/nuget/v/AzureFunctions.TestFramework.Http.svg)](https://www.nuget.org/packages/AzureFunctions.TestFramework.Http)

HTTP client support for the Azure Functions Test Framework. Provides `CreateHttpClient()` — an extension on `IFunctionsTestHost` that works like `WebApplicationFactory.CreateClient()`.

## Usage

```csharp
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Http;

public class MyFunctionTests : IAsyncLifetime
{
    private IFunctionsTestHost _testHost;
    private HttpClient _client;

    public async Task InitializeAsync()
    {
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(MyFunctions).Assembly)
            .WithHostBuilderFactory(Program.CreateHostBuilder)
            .BuildAndStartAsync();

        _client = _testHost.CreateHttpClient();
    }

    [Fact]
    public async Task GetTodos_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/todos");
        response.EnsureSuccessStatusCode();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _testHost.StopAsync();
        _testHost.Dispose();
    }
}
```

`CreateHttpClient()` auto-detects the worker mode:

- **Direct gRPC mode** (`ConfigureFunctionsWorkerDefaults()`): requests are dispatched via the gRPC `InvocationRequest` channel — no TCP port is opened.
- **ASP.NET Core integration mode** (`ConfigureFunctionsWebApplication()`): requests are forwarded to the worker's in-memory `TestServer` — no TCP port is opened.

The returned `HttpClient` has `BaseAddress` set to `http://localhost/{routePrefix}/` (custom route prefixes from `host.json` are auto-detected).

## References

- [Full documentation](https://github.com/bjorkstromm/azure-functions-test-framework)
- [`AzureFunctions.TestFramework.Core`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Core)

## License

MIT
