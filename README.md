# Azure Functions Test Framework

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

An integration testing framework for Azure Functions (dotnet-isolated) that provides a TestServer/WebApplicationFactory-like experience.

## ⚠️ Project Status: Early Development

**Current Status**: Core infrastructure is complete and the worker connects successfully to the gRPC server, but there's a critical blocker preventing function invocation. See [KNOWN_ISSUES.md](KNOWN_ISSUES.md) for details.

### What Works ✅
- gRPC server starts and accepts connections
- Azure Functions Worker runs in-process using HostBuilder
- Worker connects successfully to gRPC server
- Bidirectional gRPC streaming functional
- HTTP client API infrastructure in place

### Current Blocker 🔴
Functions aren't being discovered by the worker. FunctionsMetadataRequest returns 0 functions, causing all invocations to fail. This appears to be related to function metadata generation by the Azure Functions Worker SDK.

**We need help solving this!** See [CONTRIBUTING.md](CONTRIBUTING.md) if you'd like to contribute.

## Goals

This framework aims to provide:
- **In-process testing**: No func.exe or external processes required
- **Fast execution**: Similar performance to ASP.NET Core TestServer
- **Familiar API**: Builder pattern like WebApplicationFactory
- **Full DI control**: Override services for testing
- **Type-safe**: Strongly-typed function invocation

## Planned Usage (Once Function Loading Works)

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
        // Act
        var response = await _client.GetAsync("/api/my-function");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<MyResult>();
        Assert.NotNull(result);
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

## Architecture

The framework consists of:

1. **FunctionsTestHost**: Main orchestrator that manages worker lifecycle
2. **GrpcHostService**: Implements Azure Functions host gRPC protocol
3. **WorkerHostService**: Starts Functions Worker in-process using HostBuilder
4. **FunctionsHttpMessageHandler**: Custom HttpMessageHandler that intercepts requests and routes to functions via gRPC

Everything runs in a single process - no external func.exe or dotnet exec processes.

## Project Structure

```
src/
  AzureFunctions.TestFramework.Core/    # Main framework
  AzureFunctions.TestFramework.Http/    # HTTP-specific functionality
  
samples/
  Sample.FunctionApp/                   # Example Azure Functions app
  
tests/
  Sample.FunctionApp.Tests/             # Integration tests
```

## Building

```bash
dotnet restore
dotnet build
```

## Testing

```bash
# All tests (currently failing due to function loading issue)
dotnet test

# Single test with detailed logging
dotnet test --filter "GetTodos_ReturnsEmptyList" --logger "console;verbosity=detailed"
```

## Contributing

We'd love your help, especially with the current blocker! See [CONTRIBUTING.md](CONTRIBUTING.md) for:
- How to get started
- Current priority: solving function loading/discovery
- Development guidelines
- Pull request process

## Known Issues

See [KNOWN_ISSUES.md](KNOWN_ISSUES.md) for detailed information about:
- Current blocker: Function loading/discovery
- What works and what doesn't
- Technical details and attempts made
- Next steps and areas to investigate

## References

- [Azure Functions Worker SDK](https://github.com/Azure/azure-functions-dotnet-worker)
- [Azure Functions RPC Protocol](https://github.com/Azure/azure-functions-language-worker-protobuf)
- [ASP.NET Core WebApplicationFactory](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests) (inspiration)

## License

MIT

## Acknowledgments

Inspired by ASP.NET Core's WebApplicationFactory and TestServer patterns.
