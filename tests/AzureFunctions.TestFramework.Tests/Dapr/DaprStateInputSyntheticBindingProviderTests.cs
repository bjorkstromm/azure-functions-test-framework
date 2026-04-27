using AzureFunctions.TestFramework.Dapr;
using System.Text.Json;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Dapr;

/// <summary>
/// Unit tests for <see cref="DaprStateInputSyntheticBindingProvider.CreateSyntheticParameter"/>.
/// </summary>
public class DaprStateInputSyntheticBindingProviderTests
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
            ["my-store/my-key"] = "hello-state"
        };
        var provider = new DaprStateInputSyntheticBindingProvider(values);
        var config = ParseConfig("""{"direction":"In","stateStore":"my-store","key":"my-key"}""");

        var result = provider.CreateSyntheticParameter("stateParam", config);

        Assert.NotNull(result);
        Assert.Equal("stateParam", result!.Name);
        Assert.Equal("hello-state", result.StringValue);
    }

    [Fact]
    public void CreateSyntheticParameter_MatchingKey_JsonMode_ReturnsJsonBinding()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["my-store/my-key"] = """{"count":42}"""
        };
        var provider = new DaprStateInputSyntheticBindingProvider(values, isJson: true);
        var config = ParseConfig("""{"direction":"In","stateStore":"my-store","key":"my-key"}""");

        var result = provider.CreateSyntheticParameter("stateParam", config);

        Assert.NotNull(result);
        Assert.Equal("""{"count":42}""", result!.Json);
    }

    [Fact]
    public void CreateSyntheticParameter_NoMatchingKey_ReturnsNull()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["other-store/other-key"] = "value"
        };
        var provider = new DaprStateInputSyntheticBindingProvider(values);
        var config = ParseConfig("""{"direction":"In","stateStore":"my-store","key":"my-key"}""");

        var result = provider.CreateSyntheticParameter("stateParam", config);

        Assert.Null(result);
    }

    [Fact]
    public void CreateSyntheticParameter_OutputDirection_ReturnsNull()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["my-store/my-key"] = "value"
        };
        var provider = new DaprStateInputSyntheticBindingProvider(values);
        var config = ParseConfig("""{"direction":"Out","stateStore":"my-store","key":"my-key"}""");

        var result = provider.CreateSyntheticParameter("stateParam", config);

        Assert.Null(result);
    }

    [Fact]
    public void CreateSyntheticParameter_NoDirectionProperty_TreatedAsInput()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["my-store/my-key"] = "value"
        };
        var provider = new DaprStateInputSyntheticBindingProvider(values);
        var config = ParseConfig("""{"stateStore":"my-store","key":"my-key"}""");

        var result = provider.CreateSyntheticParameter("stateParam", config);

        Assert.NotNull(result);
    }

    [Fact]
    public void CreateSyntheticParameter_MissingStateStore_ReturnsNull()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["my-store/my-key"] = "value"
        };
        var provider = new DaprStateInputSyntheticBindingProvider(values);
        var config = ParseConfig("""{"direction":"In","key":"my-key"}""");

        var result = provider.CreateSyntheticParameter("stateParam", config);

        Assert.Null(result);
    }

    [Fact]
    public void CreateSyntheticParameter_MissingKey_ReturnsNull()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["my-store/my-key"] = "value"
        };
        var provider = new DaprStateInputSyntheticBindingProvider(values);
        var config = ParseConfig("""{"direction":"In","stateStore":"my-store"}""");

        var result = provider.CreateSyntheticParameter("stateParam", config);

        Assert.Null(result);
    }

    [Fact]
    public void CreateSyntheticParameter_KeyLookupIsCaseInsensitive()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MY-STORE/MY-KEY"] = "value"
        };
        var provider = new DaprStateInputSyntheticBindingProvider(values);
        var config = ParseConfig("""{"direction":"In","stateStore":"my-store","key":"my-key"}""");

        var result = provider.CreateSyntheticParameter("stateParam", config);

        Assert.NotNull(result);
    }

    [Fact]
    public void BindingType_IsExpected()
    {
        var provider = new DaprStateInputSyntheticBindingProvider(new Dictionary<string, string>());
        Assert.Equal("daprState", provider.BindingType);
    }

    [Fact]
    public void Constructor_NullValues_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DaprStateInputSyntheticBindingProvider(null!));
    }
}
