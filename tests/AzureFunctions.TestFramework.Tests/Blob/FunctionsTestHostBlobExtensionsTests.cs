using AzureFunctions.TestFramework.Blob;
using AzureFunctions.TestFramework.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Blob;

/// <summary>
/// Unit tests for the internal binding-data helpers in
/// <see cref="FunctionsTestHostBlobExtensions"/>.
/// </summary>
public class FunctionsTestHostBlobExtensionsTests
{
    private static readonly FunctionRegistration FakeRegistration =
        new("fn-id-1", "BlobFunc", "blobTrigger", "myBlob");

    // ── CreateBytesBindingData ─────────────────────────────────────────────────

    [Fact]
    public void CreateBytesBindingData_WithContent_UsesBytesAndNullMetadata()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var context = new FunctionInvocationContext
        {
            TriggerType = "blobTrigger",
            InputData = { ["$blobContentBytes"] = bytes }
        };

        var binding = InvokeCreateBytesBindingData(context, FakeRegistration);

        Assert.Single(binding.InputData);
        Assert.Equal("myBlob", binding.InputData[0].Name);
        Assert.Equal(bytes, binding.InputData[0].Bytes);
        Assert.Null(binding.TriggerMetadataJson);
    }

    [Fact]
    public void CreateBytesBindingData_WithTriggerMetadata_IncludesMetadata()
    {
        var bytes = new byte[] { 5, 6, 7 };
        var metaJson = """{"BlobName":"data.txt","ContainerName":"my-container"}""";
        var context = new FunctionInvocationContext
        {
            TriggerType = "blobTrigger",
            InputData =
            {
                ["$blobContentBytes"] = bytes,
                ["$triggerMetadata"] = metaJson
            }
        };

        var binding = InvokeCreateBytesBindingData(context, FakeRegistration);

        Assert.NotNull(binding.TriggerMetadataJson);
        Assert.Equal(metaJson, binding.TriggerMetadataJson!["myBlob"]);
    }

    [Fact]
    public void CreateBytesBindingData_MissingContent_UsesEmpty()
    {
        var context = new FunctionInvocationContext { TriggerType = "blobTrigger" };

        var binding = InvokeCreateBytesBindingData(context, FakeRegistration);

        Assert.Equal(Array.Empty<byte>(), binding.InputData[0].Bytes);
    }

    // ── CreateClientBindingData ────────────────────────────────────────────────

    [Fact]
    public void CreateClientBindingData_WithJson_UsesJsonBinding()
    {
        var json = FunctionsTestHostBlobExtensions.CreateBlobClientJson("my-container", "my-blob.txt");
        var metaJson = """{"BlobName":"my-blob.txt"}""";
        var context = new FunctionInvocationContext
        {
            TriggerType = "blobTrigger",
            InputData =
            {
                ["$blobClientJson"] = json,
                ["$triggerMetadata"] = metaJson
            }
        };

        var binding = InvokeCreateClientBindingData(context, FakeRegistration);

        Assert.Single(binding.InputData);
        Assert.Equal("myBlob", binding.InputData[0].Name);
        Assert.NotNull(binding.InputData[0].Json);
        Assert.Contains(FakeBlobClientInputConverter.BindingMarker, binding.InputData[0].Json!);
        Assert.NotNull(binding.TriggerMetadataJson);
    }

    [Fact]
    public void CreateClientBindingData_MissingJson_UsesEmptyJsonObject()
    {
        var context = new FunctionInvocationContext { TriggerType = "blobTrigger" };

        var binding = InvokeCreateClientBindingData(context, FakeRegistration);

        Assert.Equal("{}", binding.InputData[0].Json);
        Assert.Null(binding.TriggerMetadataJson);
    }

    // ── CreateBlobClientJson ───────────────────────────────────────────────────

    [Fact]
    public void CreateBlobClientJson_ContainerAndBlob_ContainsMarker()
    {
        var json = FunctionsTestHostBlobExtensions.CreateBlobClientJson("my-container", "my-blob.txt");
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(FakeBlobClientInputConverter.BindingMarker,
            doc.RootElement.GetProperty("Marker").GetString());
        Assert.Equal("my-container", doc.RootElement.GetProperty("ContainerName").GetString());
        Assert.Equal("my-blob.txt", doc.RootElement.GetProperty("BlobName").GetString());
    }

    [Fact]
    public void CreateBlobClientJson_NullBlobName_BlobNameIsNull()
    {
        var json = FunctionsTestHostBlobExtensions.CreateBlobClientJson("my-container", null);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("BlobName").ValueKind);
    }

    // ── InvokeBlobAsync (content overload) validation ─────────────────────────

    [Fact]
    public async Task InvokeBlobAsync_NullContent_Throws()
    {
        var host = new FakeHost();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => FunctionsTestHostBlobExtensions.InvokeBlobAsync(host, "BlobFunc", (BinaryData)null!));
    }

    [Fact]
    public void InvokeBlobAsync_WithBlobNameAndContainer_BuildsTriggerMetadata()
    {
        var host = new FakeHost();
        var content = BinaryData.FromString("test content");
        _ = FunctionsTestHostBlobExtensions.InvokeBlobAsync(
            host, "BlobFunc", content, blobName: "my-blob.txt", containerName: "my-container");

        Assert.NotNull(host.LastContext);
        Assert.True(host.LastContext!.InputData.ContainsKey("$triggerMetadata"));
        var meta = host.LastContext.InputData["$triggerMetadata"]?.ToString()!;
        Assert.Contains("my-blob.txt", meta);
    }

    [Fact]
    public void InvokeBlobAsync_WithoutBlobName_NoTriggerMetadata()
    {
        var host = new FakeHost();
        var content = BinaryData.FromString("test content");
        _ = FunctionsTestHostBlobExtensions.InvokeBlobAsync(host, "BlobFunc", content);

        Assert.False(host.LastContext!.InputData.ContainsKey("$triggerMetadata"));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TriggerBindingData InvokeCreateBytesBindingData(
        FunctionInvocationContext ctx, FunctionRegistration reg)
    {
        var method = typeof(FunctionsTestHostBlobExtensions)
            .GetMethod("CreateBytesBindingData",
                BindingFlags.NonPublic | BindingFlags.Static)!;
        return (TriggerBindingData)method.Invoke(null, [ctx, reg])!;
    }

    private static TriggerBindingData InvokeCreateClientBindingData(
        FunctionInvocationContext ctx, FunctionRegistration reg)
    {
        var method = typeof(FunctionsTestHostBlobExtensions)
            .GetMethod("CreateClientBindingData",
                BindingFlags.NonPublic | BindingFlags.Static)!;
        return (TriggerBindingData)method.Invoke(null, [ctx, reg])!;
    }

    private sealed class FakeHost : IFunctionsTestHost
    {
        public FunctionInvocationContext? LastContext { get; private set; }

        public IFunctionInvoker Invoker => new FakeInvoker(this);
        public IServiceProvider Services => new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider();
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private sealed class FakeInvoker : IFunctionInvoker
        {
            private readonly FakeHost _host;
            public FakeInvoker(FakeHost host) => _host = host;

            public Task<FunctionInvocationResult> InvokeAsync(
                string functionName,
                FunctionInvocationContext context,
                Func<FunctionInvocationContext, FunctionRegistration, TriggerBindingData> triggerBindingFactory,
                CancellationToken cancellationToken = default)
            {
                _host.LastContext = context;
                return Task.FromResult(new FunctionInvocationResult { Success = true });
            }

            public IReadOnlyDictionary<string, Microsoft.Azure.Functions.Worker.Core.FunctionMetadata.IFunctionMetadata> GetFunctions()
                => new Dictionary<string, Microsoft.Azure.Functions.Worker.Core.FunctionMetadata.IFunctionMetadata>();
        }
    }
}
