using System.Text.Json;
using AzureFunctions.TestFramework.Sql;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Sql;

/// <summary>
/// Represents this type.
/// </summary>
public class SqlInputSyntheticBindingProviderTests
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void BindingType_IsExpected()
    {
        var provider = new SqlInputSyntheticBindingProvider(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        Assert.Equal("sql", provider.BindingType);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void CreateSyntheticParameter_MatchingCommandText_ReturnsJson()
    {
        var provider = new SqlInputSyntheticBindingProvider(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["select * from t"] = """[{"id":1}]"""
            });

        var result = provider.CreateSyntheticParameter("rows", Parse("""{"direction":"In","commandText":"select * from t"}"""));

        Assert.Equal("rows", result.Name);
        Assert.Equal("""[{"id":1}]""", result.Json);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void CreateSyntheticParameter_NotInputDirection_ReturnsNullJson()
    {
        var provider = new SqlInputSyntheticBindingProvider(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        var result = provider.CreateSyntheticParameter("rows", Parse("""{"direction":"Out"}"""));

        Assert.Equal("null", result.Json);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void CreateSyntheticParameter_MissingCommandText_ReturnsNullJson()
    {
        var provider = new SqlInputSyntheticBindingProvider(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        var result = provider.CreateSyntheticParameter("rows", Parse("""{"direction":"In"}"""));

        Assert.Equal("null", result.Json);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void Constructor_NullDictionary_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SqlInputSyntheticBindingProvider(null!));
    }

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
