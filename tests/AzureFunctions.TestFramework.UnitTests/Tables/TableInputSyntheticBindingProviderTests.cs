using AzureFunctions.TestFramework.Tables;
using System.Text.Json;
using Xunit;

namespace AzureFunctions.TestFramework.UnitTests.Tables;

/// <summary>
/// Unit tests for <see cref="TableInputSyntheticBindingProvider.CreateSyntheticParameter"/>.
/// </summary>
public class TableInputSyntheticBindingProviderTests
{
    private static JsonElement ParseConfig(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Fact]
    public void CreateSyntheticParameter_ExactKeyMatch_ReturnsMatchingJson()
    {
        var jsonByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Orders/Retail/order-1"] = """{"PartitionKey":"Retail","RowKey":"order-1"}"""
        };
        var provider = new TableInputSyntheticBindingProvider(jsonByKey);
        var config = ParseConfig("""{"direction":"In","tableName":"Orders","partitionKey":"Retail","rowKey":"order-1"}""");

        var result = provider.CreateSyntheticParameter("entity", config);

        Assert.Equal("""{"PartitionKey":"Retail","RowKey":"order-1"}""", result.Json);
    }

    [Fact]
    public void CreateSyntheticParameter_PartitionKeyMatch_ReturnsMatchingJson()
    {
        var jsonByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Products/Electronics"] = """[{"PartitionKey":"Electronics"}]"""
        };
        var provider = new TableInputSyntheticBindingProvider(jsonByKey);
        var config = ParseConfig("""{"direction":"In","tableName":"Products","partitionKey":"Electronics"}""");

        var result = provider.CreateSyntheticParameter("entities", config);

        Assert.Equal("""[{"PartitionKey":"Electronics"}]""", result.Json);
    }

    [Fact]
    public void CreateSyntheticParameter_TableNameOnlyMatch_ReturnsMatchingJson()
    {
        var jsonByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Customers"] = """[{"PartitionKey":"A"},{"PartitionKey":"B"}]"""
        };
        var provider = new TableInputSyntheticBindingProvider(jsonByKey);
        var config = ParseConfig("""{"direction":"In","tableName":"Customers"}""");

        var result = provider.CreateSyntheticParameter("list", config);

        Assert.Equal("""[{"PartitionKey":"A"},{"PartitionKey":"B"}]""", result.Json);
    }

    [Fact]
    public void CreateSyntheticParameter_NoMatch_ReturnsEmptyObject()
    {
        var provider = new TableInputSyntheticBindingProvider(new Dictionary<string, string>());
        var config = ParseConfig("""{"direction":"In","tableName":"Unknown"}""");

        var result = provider.CreateSyntheticParameter("entity", config);

        Assert.Equal("{}", result.Json);
    }

    [Fact]
    public void CreateSyntheticParameter_MissingTableName_ReturnsEmptyObject()
    {
        var provider = new TableInputSyntheticBindingProvider(new Dictionary<string, string>());
        var config = ParseConfig("""{"direction":"In"}""");

        var result = provider.CreateSyntheticParameter("entity", config);

        Assert.Equal("{}", result.Json);
    }

    [Fact]
    public void CreateSyntheticParameter_OutputDirection_ReturnsEmptyObject()
    {
        var jsonByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Orders"] = """[{"PartitionKey":"A"}]"""
        };
        var provider = new TableInputSyntheticBindingProvider(jsonByKey);
        var config = ParseConfig("""{"direction":"Out","tableName":"Orders"}""");

        var result = provider.CreateSyntheticParameter("entity", config);

        Assert.Equal("{}", result.Json);
    }

    [Fact]
    public void CreateSyntheticParameter_ExactKeyPreferredOverPartition()
    {
        var jsonByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["T/PK/RK"] = """{"exact":true}""",
            ["T/PK"] = """{"partition":true}""",
            ["T"] = """{"table":true}"""
        };
        var provider = new TableInputSyntheticBindingProvider(jsonByKey);
        var config = ParseConfig("""{"direction":"In","tableName":"T","partitionKey":"PK","rowKey":"RK"}""");

        var result = provider.CreateSyntheticParameter("e", config);

        Assert.Equal("""{"exact":true}""", result.Json);
    }

    [Fact]
    public void CreateSyntheticParameter_PartitionPreferredOverTable()
    {
        var jsonByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["T/PK"] = """{"partition":true}""",
            ["T"] = """{"table":true}"""
        };
        var provider = new TableInputSyntheticBindingProvider(jsonByKey);
        // rowKey absent — falls through to partition scope
        var config = ParseConfig("""{"direction":"In","tableName":"T","partitionKey":"PK"}""");

        var result = provider.CreateSyntheticParameter("e", config);

        Assert.Equal("""{"partition":true}""", result.Json);
    }

    [Fact]
    public void CreateSyntheticParameter_FallsThroughToTableWhenNoPartitionMatch()
    {
        var jsonByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["T"] = """{"table":true}"""
        };
        var provider = new TableInputSyntheticBindingProvider(jsonByKey);
        var config = ParseConfig("""{"direction":"In","tableName":"T","partitionKey":"PK","rowKey":"RK"}""");

        var result = provider.CreateSyntheticParameter("e", config);

        Assert.Equal("""{"table":true}""", result.Json);
    }

    [Fact]
    public void CreateSyntheticParameter_LookupIsCaseInsensitive()
    {
        var jsonByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ORDERS/RETAIL/ORDER-1"] = """{"found":true}"""
        };
        var provider = new TableInputSyntheticBindingProvider(jsonByKey);
        var config = ParseConfig("""{"direction":"In","tableName":"orders","partitionKey":"retail","rowKey":"order-1"}""");

        var result = provider.CreateSyntheticParameter("e", config);

        Assert.Equal("""{"found":true}""", result.Json);
    }

    [Fact]
    public void BindingType_IsExpected()
    {
        var provider = new TableInputSyntheticBindingProvider(new Dictionary<string, string>());
        Assert.Equal("table", provider.BindingType);
    }

    [Fact]
    public void Constructor_NullValues_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TableInputSyntheticBindingProvider(null!));
    }
}
