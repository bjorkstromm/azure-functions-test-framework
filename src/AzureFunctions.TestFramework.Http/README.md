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

## Mixed bindings (HTTP + output bindings)

If a function mixes an HTTP trigger with additional output bindings (e.g. `[QueueOutput]`,
`[BlobOutput]`) on the same multi-output result type, those non-HTTP outputs can be read via
`HttpResponseMessage.GetOutputData()`:

```csharp
public sealed class CreateItemResult
{
    [HttpResult]
    public HttpResponseData HttpResponse { get; set; } = default!;

    [QueueOutput("item-created-queue")]
    public string QueueMessage { get; set; } = string.Empty;
}

var response = await client.PostAsync("/api/items", content, cancellationToken);
var outputData = response.GetOutputData();
var queueMessage = outputData["QueueMessage"]?.ToString();
```

`GetOutputData()` works in both **direct gRPC mode** (`ConfigureFunctionsWorkerDefaults()`) and
**ASP.NET Core integration mode** (`ConfigureFunctionsWebApplication()`), with either host builder API.
The HTTP output binding itself is excluded from the returned dictionary.

### `$return` as a non-HTTP output

If `[QueueOutput]`/`[BlobOutput]`/etc. is declared directly on an HTTP-triggered method (rather than
on a property of a multi-output result type), the worker binds the method's return value itself to
`$return`:

```csharp
[Function("MyQueueFunction")]
[QueueOutput("my-queue")]
public async Task<string> Run([HttpTrigger(...)] HttpRequestData req)
{
    return "Processed data";
}
```

In this case `GetOutputData()` returns a single entry keyed `"$return"` with the returned value.

### Buffering

In ASP.NET Core integration mode, requests whose functions declare additional output bindings (including
a non-HTTP `$return`) are buffered until the worker's gRPC `InvocationResponse` arrives, even with
`ResponseHeadersRead`. This buffering is driven by the function's metadata and applies to every call to
that route, whether or not the caller reads `GetOutputData()`. Waiting respects the invocation timeout
and request cancellation. Functions without additional outputs retain their normal streaming behavior.
No changes to function code or injected fake clients are required.

## References

- [Full documentation](https://github.com/bjorkstromm/azure-functions-test-framework)
- [`AzureFunctions.TestFramework.Core`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Core)

## License

MIT
