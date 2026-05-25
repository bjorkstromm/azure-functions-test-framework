using AzureFunctions.TestFramework.Blob;
using System.Text.Json;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Blob;

/// <summary>
/// Unit tests for <see cref="BlobInputSyntheticBindingProvider"/>.
/// </summary>
public class BlobInputSyntheticBindingProviderTests
{
    [Fact]
    public void BindingType_ReturnsBlob()
    {
        var provider = new BlobInputSyntheticBindingProvider(
            new Dictionary<string, BinaryData>(StringComparer.OrdinalIgnoreCase));
        Assert.Equal("blob", provider.BindingType);
    }

    [Fact]
    public void CreateSyntheticParameter_MatchingPath_ReturnsBytesBinding()
    {
        var content = BinaryData.FromString("file content");
        var provider = new BlobInputSyntheticBindingProvider(
            new Dictionary<string, BinaryData>(StringComparer.OrdinalIgnoreCase)
            {
                ["my-container/data.txt"] = content
            });

        var config = BuildConfig("blob", direction: "In", blobPath: "my-container/data.txt");
        var result = provider.CreateSyntheticParameter("blobParam", config);

        Assert.NotNull(result);
        Assert.Equal("blobParam", result!.Name);
        Assert.Equal(content.ToArray(), result.Bytes);
    }

    [Fact]
    public void CreateSyntheticParameter_CaseInsensitivePath_Matches()
    {
        var content = BinaryData.FromBytes(new byte[] { 1, 2, 3 });
        var provider = new BlobInputSyntheticBindingProvider(
            new Dictionary<string, BinaryData>(StringComparer.OrdinalIgnoreCase)
            {
                ["My-Container/Data.TXT"] = content
            });

        var config = BuildConfig("blob", direction: "In", blobPath: "my-container/data.txt");
        var result = provider.CreateSyntheticParameter("blobParam", config);

        Assert.NotNull(result);
    }

    [Fact]
    public void CreateSyntheticParameter_OutDirection_ReturnsNull()
    {
        var provider = new BlobInputSyntheticBindingProvider(
            new Dictionary<string, BinaryData>(StringComparer.OrdinalIgnoreCase)
            {
                ["my-container/data.txt"] = BinaryData.FromString("x")
            });

        var config = BuildConfig("blob", direction: "Out", blobPath: "my-container/data.txt");
        var result = provider.CreateSyntheticParameter("blobParam", config);

        Assert.Null(result);
    }

    [Fact]
    public void CreateSyntheticParameter_NoBlobPath_ReturnsNull()
    {
        var provider = new BlobInputSyntheticBindingProvider(
            new Dictionary<string, BinaryData>(StringComparer.OrdinalIgnoreCase)
            {
                ["my-container/data.txt"] = BinaryData.FromString("x")
            });

        var config = BuildConfig("blob", direction: "In", blobPath: null);
        var result = provider.CreateSyntheticParameter("blobParam", config);

        Assert.Null(result);
    }

    [Fact]
    public void CreateSyntheticParameter_PathNotRegistered_ReturnsNull()
    {
        var provider = new BlobInputSyntheticBindingProvider(
            new Dictionary<string, BinaryData>(StringComparer.OrdinalIgnoreCase));

        var config = BuildConfig("blob", direction: "In", blobPath: "my-container/missing.txt");
        var result = provider.CreateSyntheticParameter("blobParam", config);

        Assert.Null(result);
    }

    [Fact]
    public void CreateSyntheticParameter_NullDirectionProperty_TreatedAsIn()
    {
        // If "direction" is missing, should be treated as "In" (fall-through)
        var content = BinaryData.FromString("data");
        var provider = new BlobInputSyntheticBindingProvider(
            new Dictionary<string, BinaryData>(StringComparer.OrdinalIgnoreCase)
            {
                ["container/file.txt"] = content
            });

        var config = BuildConfigNoDirection(blobPath: "container/file.txt");
        var result = provider.CreateSyntheticParameter("blobParam", config);

        Assert.NotNull(result);
    }

    [Fact]
    public void Constructor_NullContentByPath_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BlobInputSyntheticBindingProvider(null!));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static JsonElement BuildConfig(string type, string? direction, string? blobPath)
    {
        var obj = new Dictionary<string, object?> { ["type"] = type };
        if (direction != null) obj["direction"] = direction;
        if (blobPath != null) obj["blobPath"] = blobPath;
        var json = JsonSerializer.Serialize(obj);
        return JsonDocument.Parse(json).RootElement;
    }

    private static JsonElement BuildConfigNoDirection(string? blobPath)
    {
        var obj = new Dictionary<string, object?> { ["type"] = "blob" };
        if (blobPath != null) obj["blobPath"] = blobPath;
        var json = JsonSerializer.Serialize(obj);
        return JsonDocument.Parse(json).RootElement;
    }
}
