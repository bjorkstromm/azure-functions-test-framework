using AzureFunctions.TestFramework.Blob;
using System.Text.Json;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Blob;

/// <summary>
/// Unit tests for <see cref="BlobInputClientSyntheticBindingProvider"/>.
/// </summary>
public class BlobInputClientSyntheticBindingProviderTests
{
    [Fact]
    public void BindingType_ReturnsBlob()
    {
        var provider = new BlobInputClientSyntheticBindingProvider(["my-container/data.txt"]);
        Assert.Equal("blob", provider.BindingType);
    }

    [Fact]
    public void CreateSyntheticParameter_RegisteredPath_ReturnsJsonBinding()
    {
        var provider = new BlobInputClientSyntheticBindingProvider(["my-container/data.txt"]);
        var config = BuildConfig(direction: "In", blobPath: "my-container/data.txt");

        var result = provider.CreateSyntheticParameter("blobParam", config);

        Assert.NotNull(result);
        Assert.Equal("blobParam", result!.Name);
        Assert.NotNull(result.Json);
        Assert.Contains(FakeBlobClientInputConverter.BindingMarker, result.Json!);
    }

    [Fact]
    public void CreateSyntheticParameter_CaseInsensitivePath_Matches()
    {
        var provider = new BlobInputClientSyntheticBindingProvider(["MY-CONTAINER/DATA.TXT"]);
        var config = BuildConfig(direction: "In", blobPath: "my-container/data.txt");

        var result = provider.CreateSyntheticParameter("blobParam", config);

        Assert.NotNull(result);
    }

    [Fact]
    public void CreateSyntheticParameter_OutDirection_ReturnsNull()
    {
        var provider = new BlobInputClientSyntheticBindingProvider(["my-container/data.txt"]);
        var config = BuildConfig(direction: "Out", blobPath: "my-container/data.txt");

        var result = provider.CreateSyntheticParameter("blobParam", config);

        Assert.Null(result);
    }

    [Fact]
    public void CreateSyntheticParameter_UnregisteredPath_ReturnsNull()
    {
        var provider = new BlobInputClientSyntheticBindingProvider(["other-container/file.txt"]);
        var config = BuildConfig(direction: "In", blobPath: "my-container/data.txt");

        var result = provider.CreateSyntheticParameter("blobParam", config);

        Assert.Null(result);
    }

    [Fact]
    public void CreateSyntheticParameter_NoBlobPath_ReturnsNull()
    {
        var provider = new BlobInputClientSyntheticBindingProvider(["my-container/data.txt"]);
        var config = BuildConfig(direction: "In", blobPath: null);

        var result = provider.CreateSyntheticParameter("blobParam", config);

        Assert.Null(result);
    }

    [Fact]
    public void Constructor_NullPaths_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BlobInputClientSyntheticBindingProvider(null!));
    }

    // ── ParseBlobPath ─────────────────────────────────────────────────────────

    [Fact]
    public void ParseBlobPath_WithSlash_SplitsCorrectly()
    {
        BlobInputClientSyntheticBindingProvider.ParseBlobPath(
            "my-container/path/to/blob.txt",
            out var containerName, out var blobName);

        Assert.Equal("my-container", containerName);
        Assert.Equal("path/to/blob.txt", blobName);
    }

    [Fact]
    public void ParseBlobPath_NoSlash_ContainerNameOnly()
    {
        BlobInputClientSyntheticBindingProvider.ParseBlobPath(
            "my-container",
            out var containerName, out var blobName);

        Assert.Equal("my-container", containerName);
        Assert.Null(blobName);
    }

    [Fact]
    public void ParseBlobPath_SingleLevelBlob_ExtractsCorrectly()
    {
        BlobInputClientSyntheticBindingProvider.ParseBlobPath(
            "container/blob.txt",
            out var containerName, out var blobName);

        Assert.Equal("container", containerName);
        Assert.Equal("blob.txt", blobName);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static JsonElement BuildConfig(string? direction, string? blobPath)
    {
        var obj = new Dictionary<string, object?> { ["type"] = "blob" };
        if (direction != null) obj["direction"] = direction;
        if (blobPath != null) obj["blobPath"] = blobPath;
        var json = JsonSerializer.Serialize(obj);
        return JsonDocument.Parse(json).RootElement;
    }
}
