# Azure Functions Test Framework

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

An integration testing framework for Azure Functions (dotnet-isolated) that provides a TestServer/WebApplicationFactory-like experience.

## ⚠️ Project Status: Early Development

**Current Status**: Both testing approaches are fully functional. The gRPC-based `FunctionsTestHost` supports full CRUD HTTP invocations (11/11 tests pass). `FunctionsWebApplicationFactory` supports full CRUD including POST/PUT/DELETE and `WithWebHostBuilder` service overrides (4/4 tests pass). See [KNOWN_ISSUES.md](KNOWN_ISSUES.md) for details.

### What Works ✅
- gRPC server starts and accepts connections
- Azure Functions Worker runs in-process using HostBuilder
- Worker connects successfully to gRPC server and loads all functions
- Bidirectional gRPC streaming functional
- HTTP client API (`FunctionsTestHostBuilder` + `CreateHttpClient()`) functional for all HTTP methods (GET, POST, PUT, DELETE)
- `FunctionsWebApplicationFactory<TProgram>` functional for all HTTP methods via ASP.NET Core `TestServer`
- Route matching with `{param}` placeholder support in both approaches
- `WithWebHostBuilder` DI service overrides work end-to-end
- Tests run in parallel (xUnit `parallelizeTestCollections`) and in isolation (per-test host via `IAsyncLifetime` for gRPC tests; shared `IClassFixture` factory + per-test `InMemoryTodoService.Reset()` for WAF tests)
- gRPC EventStream shuts down gracefully on test teardown (no connection-abort errors, no Kestrel 5 s shutdown wait)

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

> ✅ **Status**: Fully functional for all HTTP methods (GET, POST, PUT, DELETE) and `WithWebHostBuilder` DI overrides.

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

**Usage** (with per-test isolation via `IAsyncLifetime`):
```csharp
// Reference: AzureFunctions.TestFramework.AspNetCore
public class MyFunctionTests : IAsyncLifetime
{
    private FunctionsWebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        _factory = new FunctionsWebApplicationFactory<Program>();
        _client = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Health_ReturnsOk()
        => (await _client!.GetAsync("/api/health")).EnsureSuccessStatusCode();
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
  Sample.FunctionApp.Tests/                         # gRPC-based integration tests (FunctionsTestHost)
  Sample.FunctionApp.WebApplicationFactory.Tests/   # WebApplicationFactory-based integration tests
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

# gRPC-based tests only
dotnet test tests/Sample.FunctionApp.Tests

# WebApplicationFactory-based tests only
dotnet test tests/Sample.FunctionApp.WebApplicationFactory.Tests

# Single test with detailed logging
dotnet test --filter "GetTodos_ReturnsEmptyList" --logger "console;verbosity=detailed"
```

## Known Issues

See [KNOWN_ISSUES.md](KNOWN_ISSUES.md) for detailed information about:
- `FunctionsWebApplicationFactory` POST/PUT function ID mismatch
- What works and what doesn't

## References

- [Azure Functions Worker SDK](https://github.com/Azure/azure-functions-dotnet-worker)
- [Azure Functions RPC Protocol](https://github.com/Azure/azure-functions-language-worker-protobuf)
- [ASP.NET Core WebApplicationFactory](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests) (inspiration)

## License

MIT

