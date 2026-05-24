using AzureFunctions.TestFramework.Dapr;
using System.Text.Json;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Dapr;

/// <summary>
/// Unit tests for <see cref="DaprSecretInputSyntheticBindingProvider.CreateSyntheticParameter"/>.
/// </summary>
public class DaprSecretInputSyntheticBindingProviderTests
{
    private static JsonElement ParseConfig(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Fact]
    public void CreateSyntheticParameter_MatchingKey_ReturnsStringBinding()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["my-secrets/api-key"] = "super-secret"
        };
        var provider = new DaprSecretInputSyntheticBindingProvider(values);
        var config = ParseConfig("""{"direction":"In","secretStoreName":"my-secrets","key":"api-key"}""");

        var result = provider.CreateSyntheticParameter("secretParam", config);

        Assert.NotNull(result);
        Assert.Equal("secretParam", result!.Name);
        Assert.Equal("super-secret", result.StringValue);
    }

    [Fact]
    public void CreateSyntheticParameter_MatchingKey_JsonMode_ReturnsJsonBinding()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["my-secrets/api-key"] = """{"value":"secret"}"""
        };
        var provider = new DaprSecretInputSyntheticBindingProvider(values, isJson: true);
        var config = ParseConfig("""{"direction":"In","secretStoreName":"my-secrets","key":"api-key"}""");

        var result = provider.CreateSyntheticParameter("secretParam", config);

        Assert.NotNull(result);
        Assert.Equal("""{"value":"secret"}""", result!.Json);
    }

    [Fact]
    public void CreateSyntheticParameter_NoMatchingKey_ReturnsNull()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["other-store/other-key"] = "value"
        };
        var provider = new DaprSecretInputSyntheticBindingProvider(values);
        var config = ParseConfig("""{"direction":"In","secretStoreName":"my-secrets","key":"api-key"}""");

        var result = provider.CreateSyntheticParameter("secretParam", config);

        Assert.Null(result);
    }

    [Fact]
    public void CreateSyntheticParameter_OutputDirection_ReturnsNull()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["my-secrets/api-key"] = "value"
        };
        var provider = new DaprSecretInputSyntheticBindingProvider(values);
        var config = ParseConfig("""{"direction":"Out","secretStoreName":"my-secrets","key":"api-key"}""");

        var result = provider.CreateSyntheticParameter("secretParam", config);

        Assert.Null(result);
    }

    [Fact]
    public void CreateSyntheticParameter_ReturnDirection_ReturnsNull()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["my-secrets/api-key"] = "value"
        };
        var provider = new DaprSecretInputSyntheticBindingProvider(values);
        var config = ParseConfig("""{"direction":"Return","secretStoreName":"my-secrets","key":"api-key"}""");

        var result = provider.CreateSyntheticParameter("secretParam", config);

        Assert.Null(result);
    }

    [Fact]
    public void CreateSyntheticParameter_NoDirectionProperty_TreatedAsInput()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["my-secrets/api-key"] = "value"
        };
        var provider = new DaprSecretInputSyntheticBindingProvider(values);
        var config = ParseConfig("""{"secretStoreName":"my-secrets","key":"api-key"}""");

        var result = provider.CreateSyntheticParameter("secretParam", config);

        Assert.NotNull(result);
    }

    [Fact]
    public void CreateSyntheticParameter_MissingSecretStoreName_ReturnsNull()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["my-secrets/api-key"] = "value"
        };
        var provider = new DaprSecretInputSyntheticBindingProvider(values);
        var config = ParseConfig("""{"direction":"In","key":"api-key"}""");

        var result = provider.CreateSyntheticParameter("secretParam", config);

        Assert.Null(result);
    }

    [Fact]
    public void CreateSyntheticParameter_MissingKey_ReturnsNull()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["my-secrets/api-key"] = "value"
        };
        var provider = new DaprSecretInputSyntheticBindingProvider(values);
        var config = ParseConfig("""{"direction":"In","secretStoreName":"my-secrets"}""");

        var result = provider.CreateSyntheticParameter("secretParam", config);

        Assert.Null(result);
    }

    [Fact]
    public void CreateSyntheticParameter_KeyLookupIsCaseInsensitive()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MY-SECRETS/API-KEY"] = "value"
        };
        var provider = new DaprSecretInputSyntheticBindingProvider(values);
        var config = ParseConfig("""{"direction":"In","secretStoreName":"my-secrets","key":"api-key"}""");

        var result = provider.CreateSyntheticParameter("secretParam", config);

        Assert.NotNull(result);
    }

    [Fact]
    public void BindingType_IsExpected()
    {
        var provider = new DaprSecretInputSyntheticBindingProvider(new Dictionary<string, string>());
        Assert.Equal("daprSecret", provider.BindingType);
    }

    [Fact]
    public void Constructor_NullValues_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DaprSecretInputSyntheticBindingProvider(null!));
    }
}
