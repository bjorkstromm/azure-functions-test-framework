# AzureFunctions.TestFramework.Blob

[![NuGet](https://img.shields.io/nuget/v/AzureFunctions.TestFramework.Blob.svg)](https://www.nuget.org/packages/AzureFunctions.TestFramework.Blob)

BlobTrigger and BlobInput support for the Azure Functions Test Framework. Provides:

- `InvokeBlobAsync(...)` — trigger blob-triggered functions directly from tests.
- `WithBlobInputContent(...)` — inject fake blob content for functions that use `[BlobInput]` (non-trigger input binding).

## BlobTrigger invocation

```csharp
using AzureFunctions.TestFramework.Blob;
using AzureFunctions.TestFramework.Core;

public class BlobFunctionTests : IAsyncLifetime
{
    private IFunctionsTestHost _testHost;

    public async Task InitializeAsync()
    {
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(MyBlobFunction).Assembly)
            .BuildAndStartAsync();
    }

    [Fact]
    public async Task ProcessBlob_WithTextContent_Succeeds()
    {
        var content = BinaryData.FromString("Hello from blob!");
        var result = await _testHost.InvokeBlobAsync("ProcessBlob", content, blobName: "test/file.txt");
        Assert.True(result.Success);
    }

    [Fact]
    public async Task ProcessBlob_WithJsonContent_Succeeds()
    {
        var content = BinaryData.FromObjectAsJson(new { id = "123", name = "test" });
        var result = await _testHost.InvokeBlobAsync(
            "ProcessBlob", content,
            blobName: "orders/order-123.json",
            containerName: "orders");
        Assert.True(result.Success);
    }

    public async Task DisposeAsync()
    {
        await _testHost.StopAsync();
        _testHost.Dispose();
    }
}
```

## `[BlobInput]` injection

The `WithBlobInputContent` builder extension injects fake blob content for functions that use the `[BlobInput]` attribute (non-trigger input binding).

```csharp
// Function under test
[Function("ReadDocument")]
public static string Run(
    [QueueTrigger("process-queue")] string blobPath,
    [BlobInput("documents/template.txt")] string templateContent)
{
    return $"Template: {templateContent}";
}
```

```csharp
// Test setup
using AzureFunctions.TestFramework.Blob;

_testHost = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunction).Assembly)
    // Register the content to inject for [BlobInput("documents/template.txt")]
    .WithBlobInputContent("documents/template.txt", BinaryData.FromString("Hello, template!"))
    .BuildAndStartAsync();
```

**Multiple blobs:** Use the dictionary overload to register several paths at once:

```csharp
_testHost = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunction).Assembly)
    .WithBlobInputContent(new Dictionary<string, BinaryData>
    {
        ["documents/template.txt"] = BinaryData.FromString("Hello, template!"),
        ["configs/settings.json"] = BinaryData.FromObjectAsJson(new { timeout = 30 }),
    })
    .BuildAndStartAsync();
```

The `blobPath` key must match the path declared in the `[BlobInput]` attribute exactly (case-insensitive). Dynamic path templates (e.g. `"documents/{queueTrigger}"`) are matched against the template string, not the resolved path. When no content is registered for a binding, an empty `byte[]` is injected (which the worker surfaces as `null` / empty for string and stream parameters).

> **Supported parameter types:** `string`, `byte[]`, `Stream`, `BinaryData`, `ReadOnlyMemory<byte>`. For SDK client types (`BlobClient`, `BlockBlobClient`, etc.), see the sections below.

## BlobTrigger with `BlobClient` parameter

When a blob-triggered function receives a `BlobClient` (or `BlockBlobClient`, `PageBlobClient`, etc.) instead of content bytes, register a `BlobServiceClient` on the test host and use the container/blob overload:

```csharp
using Azure.Storage.Blobs;
using AzureFunctions.TestFramework.Blob;

_testHost = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyBlobFunction).Assembly)
    .WithBlobServiceClient(new BlobServiceClient("UseDevelopmentStorage=true"))
    .BuildAndStartAsync();

// Invoke with container and blob name — the function receives a BlobClient
var result = await _testHost.InvokeBlobAsync("ProcessBlobClient", "my-container", "my-blob.txt");
Assert.True(result.Success);
```

## `[BlobInput]` with `BlobClient` parameter

For `[BlobInput]` bindings targeting SDK client types, also register the blob path with `WithBlobInputClient`:

```csharp
_testHost = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunction).Assembly)
    .WithBlobServiceClient(new BlobServiceClient("UseDevelopmentStorage=true"))
    .WithBlobInputClient("my-container/data.txt")
    .BuildAndStartAsync();
```

Supported client parameter types: `BlobClient`, `BlockBlobClient`, `PageBlobClient`, `AppendBlobClient`, `BlobBaseClient`, `BlobContainerClient`.

## References

- [Full documentation](https://github.com/bjorkstromm/azure-functions-test-framework)
- [`AzureFunctions.TestFramework.Core`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Core)

## License

MIT
