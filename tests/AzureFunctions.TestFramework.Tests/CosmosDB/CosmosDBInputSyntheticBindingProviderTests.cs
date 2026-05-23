using System.Text.Json;
using AzureFunctions.TestFramework.CosmosDB;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.CosmosDB;

public class CosmosDBInputSyntheticBindingProviderTests
{
    [Fact]
    public void BindingType_IsExpected()
    {
        var provider = new CosmosDBInputSyntheticBindingProvider(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        Assert.Equal("cosmosDB", provider.BindingType);
    }

    [Fact]
    public void CreateSyntheticParameter_MatchingKey_ReturnsJson()
    {
        var provider = new CosmosDBInputSyntheticBindingProvider(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["db1/c1"] = """{"id":"1"}"""
            });

        var result = provider.CreateSyntheticParameter("docs", Parse("""{"direction":"In","databaseName":"db1","containerName":"c1"}"""));

        Assert.Equal("docs", result.Name);
        Assert.Equal("""{"id":"1"}""", result.Json);
    }

    [Fact]
    public void CreateSyntheticParameter_OutputDirection_ReturnsNullJson()
    {
        var provider = new CosmosDBInputSyntheticBindingProvider(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        var result = provider.CreateSyntheticParameter("docs", Parse("""{"direction":"Out"}"""));

        Assert.Equal("null", result.Json);
    }

    [Fact]
    public void CreateSyntheticParameter_MissingMapping_ReturnsNullJson()
    {
        var provider = new CosmosDBInputSyntheticBindingProvider(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        var result = provider.CreateSyntheticParameter("docs", Parse("""{"direction":"In","databaseName":"db1","containerName":"missing"}"""));

        Assert.Equal("null", result.Json);
    }

    [Fact]
    public void Constructor_NullDictionary_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new CosmosDBInputSyntheticBindingProvider(null!));
    }

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
