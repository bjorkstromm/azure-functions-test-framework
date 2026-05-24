using System.Text.Json;
using AzureFunctions.TestFramework.DataExplorer;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.DataExplorer;

/// <summary>
/// Represents this type.
/// </summary>
public class KustoInputSyntheticBindingProviderTests
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void BindingType_IsExpected()
    {
        var provider = new KustoInputSyntheticBindingProvider(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        Assert.Equal("kusto", provider.BindingType);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void CreateSyntheticParameter_MatchingKey_ReturnsJson()
    {
        var provider = new KustoInputSyntheticBindingProvider(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["db1/InputTable"] = """[{"id":1}]"""
            });

        var result = provider.CreateSyntheticParameter("rows", Parse("""{"direction":"In","database":"db1","kqlCommand":"InputTable | take 1"}"""));

        Assert.Equal("rows", result.Name);
        Assert.Equal("""[{"id":1}]""", result.Json);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void CreateSyntheticParameter_OutputDirection_ReturnsNullJson()
    {
        var provider = new KustoInputSyntheticBindingProvider(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        var result = provider.CreateSyntheticParameter("rows", Parse("""{"direction":"Out"}"""));

        Assert.Equal("null", result.Json);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void CreateSyntheticParameter_MissingMapping_ReturnsNullJson()
    {
        var provider = new KustoInputSyntheticBindingProvider(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        var result = provider.CreateSyntheticParameter("rows", Parse("""{"direction":"In","database":"db1","kqlCommand":"OtherTable | take 1"}"""));

        Assert.Equal("null", result.Json);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void Constructor_NullDictionary_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new KustoInputSyntheticBindingProvider(null!));
    }

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
