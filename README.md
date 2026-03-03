# Azure Functions Test Framework

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

An integration testing framework for Azure Functions (dotnet-isolated) that provides a TestServer/WebApplicationFactory-like experience.

## ⚠️ Project Status: Early Development

**Current Status**: Core infrastructure is complete. The gRPC worker connects successfully, function discovery works, and HTTP-triggered functions can be invoked. A second approach using `WebApplicationFactory` directly is under active development. See [KNOWN_ISSUES.md](KNOWN_ISSUES.md) for details.

### What Works ✅
- gRPC server starts and accepts connections
- Azure Functions Worker runs in-process using HostBuilder
- Worker connects successfully to gRPC server and loads all functions
- Bidirectional gRPC streaming functional
- HTTP client API (`FunctionsTestHostBuilder` + `CreateHttpClient()`) functional for GET requests
- `FunctionsWebApplicationFactory<TProgram>` scaffolded (ASP.NET Core integration, see below)

### Current Blockers 🔴
- POST/PUT request body parsing fails (body must be sent as bytes, not string — see [KNOWN_ISSUES.md](KNOWN_ISSUES.md))
- `FunctionsWebApplicationFactory` hangs during host startup (under investigation — see [KNOWN_ISSUES.md](KNOWN_ISSUES.md))

## Goals

This framework aims to provide:
- **In-process testing**: No func.exe or external processes required
- **Fast execution**: Similar performance to ASP.NET Core TestServer
- **Two testing approaches**: gRPC-based (`FunctionsTestHost`) and ASP.NET Core pipeline-based (`FunctionsWebApplicationFactory`)
- **Full DI control**: Override services for testing
- **Middleware support**: Test middleware registered in `Program.cs`

## Approaches

### 1. FunctionsTestHost (gRPC-based)

Uses a custom gRPC host that mimics the Azure Functions host, starting the worker in-process and sending invocations directly via gRPC.

```csharp
public class MyFunctionTests : IAsyncLifetime
{
    private IFunctionsTestHost _testHost;
    private HttpClient _client;

    public async Task InitializeAsync()
    {
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(MyFunctions).Assembly)
            .ConfigureServices(services =>
            {
                // Override dependencies for testing
                services.AddSingleton<IMyService, MockMyService>();
            })
            .BuildAndStartAsync();
            
        _client = _testHost.CreateHttpClient();
    }

    [Fact]
    public async Task MyFunction_ReturnsExpectedResult()
    {
        var response = await _client.GetAsync("/api/my-function");
        response.EnsureSuccessStatusCode();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_testHost != null)
        {
            await _testHost.StopAsync();
            _testHost.Dispose();
        }
    }
}
```

### 2. FunctionsWebApplicationFactory (ASP.NET Core pipeline)

Uses `WebApplicationFactory<TProgram>` directly, running the full ASP.NET Core pipeline from `Program.cs` — including custom middleware and services — through `TestServer`. Requires the function app to use `ConfigureFunctionsWebApplication()`.

> ⚠️ **Status**: Scaffolded and partially working. Host startup currently hangs. See [KNOWN_ISSUES.md](KNOWN_ISSUES.md).

**Setup** — add to `Program.cs`:
```csharp
public partial class Program
{
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWebApplication()
            .ConfigureServices(services => { /* ... */ });
}

await Program.CreateHostBuilder(args).Build().RunAsync();
```

**Usage**:
```csharp
// Reference: AzureFunctions.TestFramework.AspNetCore
public class MyFunctionTests : IClassFixture<FunctionsWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public MyFunctionTests(FunctionsWebApplicationFactory<Program> factory)
    {
        // Override services from Program.cs for testing
        _client = factory.WithWebHostBuilder(b =>
            b.ConfigureServices(s => s.AddSingleton<IMyService, FakeMyService>()))
            .CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
        => (await _client.GetAsync("/api/health")).EnsureSuccessStatusCode();
}
```

## Architecture

```
src/
  AzureFunctions.TestFramework.Core/       # gRPC host, worker hosting, HTTP invocation
  AzureFunctions.TestFramework.AspNetCore/ # WebApplicationFactory-based testing
  AzureFunctions.TestFramework.Http/       # HTTP-specific functionality (placeholder)
  
samples/
  Sample.FunctionApp/                      # Example Azure Functions app (TodoAPI)
  
tests/
  Sample.FunctionApp.Tests/               # Integration tests for both approaches
```

## Building

```bash
dotnet restore
dotnet build
```

## Testing

```bash
# All tests
dotnet test

# Single test with detailed logging
dotnet test --filter "GetTodos_ReturnsEmptyList" --logger "console;verbosity=detailed"
```

## Known Issues

See [KNOWN_ISSUES.md](KNOWN_ISSUES.md) for detailed information about:
- POST/PUT request body parsing blocker
- `FunctionsWebApplicationFactory` host startup hang
- What works and what doesn't

## References

- [Azure Functions Worker SDK](https://github.com/Azure/azure-functions-dotnet-worker)
- [Azure Functions RPC Protocol](https://github.com/Azure/azure-functions-language-worker-protobuf)
- [ASP.NET Core WebApplicationFactory](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests) (inspiration)

## License

MIT

