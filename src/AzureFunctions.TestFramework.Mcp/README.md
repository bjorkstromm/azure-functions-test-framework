# AzureFunctions.TestFramework.Mcp

[![NuGet](https://img.shields.io/nuget/v/AzureFunctions.TestFramework.Mcp.svg)](https://www.nuget.org/packages/AzureFunctions.TestFramework.Mcp)

MCP (Model Context Protocol) trigger invocation support for the Azure Functions Test Framework. Provides `InvokeMcpToolAsync(...)`, `InvokeMcpResourceAsync(...)`, and `InvokeMcpPromptAsync(...)` — extensions on `IFunctionsTestHost` that let you trigger MCP-triggered functions directly from integration tests.

## Usage

### McpToolTrigger

```csharp
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Mcp;

[Fact]
public async Task McpTool_WithArguments_Succeeds()
{
    var result = await _testHost.InvokeMcpToolAsync(
        "MyTool",
        toolArguments: new Dictionary<string, object> { ["input"] = "hello" });

    Assert.True(result.Success);
}
```

### McpResourceTrigger

```csharp
[Fact]
public async Task McpResource_WithUri_Succeeds()
{
    var result = await _testHost.InvokeMcpResourceAsync(
        "MyResource",
        resourceUri: "resource://my-resource/item-1");

    Assert.True(result.Success);
}
```

### McpPromptTrigger

```csharp
[Fact]
public async Task McpPrompt_WithArguments_Succeeds()
{
    var result = await _testHost.InvokeMcpPromptAsync(
        "MyPrompt",
        arguments: new Dictionary<string, string> { ["topic"] = "testing" });

    Assert.True(result.Success);
}
```

### API

```csharp
// McpToolTrigger
Task<FunctionInvocationResult> InvokeMcpToolAsync(
    this IFunctionsTestHost host,
    string functionName,
    IReadOnlyDictionary<string, object>? toolArguments = null,
    string? toolName = null,
    string? sessionId = null,
    CancellationToken cancellationToken = default)

// McpResourceTrigger
Task<FunctionInvocationResult> InvokeMcpResourceAsync(
    this IFunctionsTestHost host,
    string functionName,
    string resourceUri,
    string? sessionId = null,
    CancellationToken cancellationToken = default)

// McpPromptTrigger
Task<FunctionInvocationResult> InvokeMcpPromptAsync(
    this IFunctionsTestHost host,
    string functionName,
    IReadOnlyDictionary<string, string>? arguments = null,
    string? promptName = null,
    string? sessionId = null,
    CancellationToken cancellationToken = default)
```

- **`functionName`** — the name of the MCP function (case-insensitive).
- **`toolArguments`** / **`arguments`** — optional named arguments to pass to the tool or prompt.
- **`resourceUri`** — the URI of the MCP resource to trigger.
- **`toolName`** / **`promptName`** — optional name override; defaults to `functionName` when `null`.
- **`sessionId`** — optional MCP session ID; a new GUID is generated when `null`.

### Output binding capture

Use `FunctionInvocationResult` to inspect output bindings produced by the function:

```csharp
var result = await _testHost.InvokeMcpToolAsync("MyTool",
    toolArguments: new Dictionary<string, object> { ["input"] = "hello" });

Assert.True(result.Success);

// Read the function return value
var returnValue = result.ReadReturnValueAs<string>();
Assert.Equal("expected output", returnValue);
```

## References

- [Full documentation](https://github.com/bjorkstromm/azure-functions-test-framework)
- [`AzureFunctions.TestFramework.Core`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Core)

## License

MIT
