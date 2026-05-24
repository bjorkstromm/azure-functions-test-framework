using System.Reflection;
using System.Text.Json;
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Tables;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Tables;

/// <summary>
/// Unit tests for <see cref="FunctionsTestHostBuilderTablesExtensions"/>.
/// </summary>
public class FunctionsTestHostBuilderTablesExtensionsTests
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithTableEntity_ReturnsSameBuilder_AndRegistersExactKeyProvider()
    {
        var builder = new FunctionsTestHostBuilder();
        var entity = new SampleEntity
        {
            PartitionKey = "Retail",
            RowKey = "order-1",
            PayloadValue = "Pending"
        };

        var result = builder.WithTableEntity("Orders", "Retail", "order-1", entity);

        Assert.Same(builder, result);

        var provider = ExtractSingleProvider(builder);
        var binding = provider.CreateSyntheticParameter(
            "entity",
            ParseConfig("""{"direction":"In","tableName":"Orders","partitionKey":"Retail","rowKey":"order-1"}"""));

        Assert.Equal(
            """{"PartitionKey":"Retail","RowKey":"order-1","PayloadValue":"Pending"}""",
            binding.Json);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithTableEntities_TableScope_ReturnsSameBuilder_AndRegistersTableProvider()
    {
        var builder = new FunctionsTestHostBuilder();
        var entities = new[]
        {
            new SampleEntity { PartitionKey = "Retail", RowKey = "1", PayloadValue = "A" },
            new SampleEntity { PartitionKey = "Retail", RowKey = "2", PayloadValue = "B" }
        };

        var result = builder.WithTableEntities("Orders", entities);

        Assert.Same(builder, result);

        var provider = ExtractSingleProvider(builder);
        var binding = provider.CreateSyntheticParameter(
            "entities",
            ParseConfig("""{"direction":"In","tableName":"Orders"}"""));

        Assert.Equal(
            """[{"PartitionKey":"Retail","RowKey":"1","PayloadValue":"A"},{"PartitionKey":"Retail","RowKey":"2","PayloadValue":"B"}]""",
            binding.Json);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithTableEntities_PartitionScope_ReturnsSameBuilder_AndRegistersPartitionProvider()
    {
        var builder = new FunctionsTestHostBuilder();
        var entities = new[]
        {
            new SampleEntity { PartitionKey = "Retail", RowKey = "1", PayloadValue = "A" }
        };

        var result = builder.WithTableEntities("Orders", "Retail", entities);

        Assert.Same(builder, result);

        var provider = ExtractSingleProvider(builder);
        var binding = provider.CreateSyntheticParameter(
            "entities",
            ParseConfig("""{"direction":"In","tableName":"Orders","partitionKey":"Retail"}"""));

        Assert.Equal(
            """[{"PartitionKey":"Retail","RowKey":"1","PayloadValue":"A"}]""",
            binding.Json);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithTableInputData_ReturnsSameBuilder_AndUsesProvidedJsonMap()
    {
        var builder = new FunctionsTestHostBuilder();
        IReadOnlyDictionary<string, string> jsonByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Orders/Retail/order-1"] = """{"PayloadValue":"from-map"}"""
        };

        var result = builder.WithTableInputData(jsonByKey);

        Assert.Same(builder, result);

        var provider = ExtractSingleProvider(builder);
        var binding = provider.CreateSyntheticParameter(
            "entity",
            ParseConfig("""{"direction":"In","tableName":"Orders","partitionKey":"Retail","rowKey":"order-1"}"""));

        Assert.Equal("""{"PayloadValue":"from-map"}""", binding.Json);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithTableEntity_NullBuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            FunctionsTestHostBuilderTablesExtensions.WithTableEntity<SampleEntity>(
                null!,
                "Orders",
                "Retail",
                "order-1",
                new SampleEntity()));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithTableEntity_EmptyTableName_Throws()
    {
        var builder = new FunctionsTestHostBuilder();

        Assert.Throws<ArgumentException>(() =>
            builder.WithTableEntity(
                string.Empty,
                "Retail",
                "order-1",
                new SampleEntity()));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithTableEntity_EmptyPartitionKey_Throws()
    {
        var builder = new FunctionsTestHostBuilder();

        Assert.Throws<ArgumentException>(() =>
            builder.WithTableEntity(
                "Orders",
                string.Empty,
                "order-1",
                new SampleEntity()));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithTableEntity_EmptyRowKey_Throws()
    {
        var builder = new FunctionsTestHostBuilder();

        Assert.Throws<ArgumentException>(() =>
            builder.WithTableEntity(
                "Orders",
                "Retail",
                string.Empty,
                new SampleEntity()));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithTableEntity_NullEntity_Throws()
    {
        var builder = new FunctionsTestHostBuilder();

        Assert.Throws<ArgumentNullException>(() =>
            builder.WithTableEntity<SampleEntity>(
                "Orders",
                "Retail",
                "order-1",
                null!));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithTableEntities_TableScope_NullBuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            FunctionsTestHostBuilderTablesExtensions.WithTableEntities<SampleEntity>(
                null!,
                "Orders",
                Array.Empty<SampleEntity>()));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithTableEntities_TableScope_EmptyTableName_Throws()
    {
        var builder = new FunctionsTestHostBuilder();

        Assert.Throws<ArgumentException>(() =>
            builder.WithTableEntities(string.Empty, Array.Empty<SampleEntity>()));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithTableEntities_TableScope_NullEntities_Throws()
    {
        var builder = new FunctionsTestHostBuilder();

        Assert.Throws<ArgumentNullException>(() =>
            builder.WithTableEntities<SampleEntity>("Orders", null!));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithTableEntities_PartitionScope_NullBuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            FunctionsTestHostBuilderTablesExtensions.WithTableEntities<SampleEntity>(
                null!,
                "Orders",
                "Retail",
                Array.Empty<SampleEntity>()));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithTableEntities_PartitionScope_EmptyTableName_Throws()
    {
        var builder = new FunctionsTestHostBuilder();

        Assert.Throws<ArgumentException>(() =>
            builder.WithTableEntities(string.Empty, "Retail", Array.Empty<SampleEntity>()));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithTableEntities_PartitionScope_EmptyPartitionKey_Throws()
    {
        var builder = new FunctionsTestHostBuilder();

        Assert.Throws<ArgumentException>(() =>
            builder.WithTableEntities("Orders", string.Empty, Array.Empty<SampleEntity>()));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithTableEntities_PartitionScope_NullEntities_Throws()
    {
        var builder = new FunctionsTestHostBuilder();

        Assert.Throws<ArgumentNullException>(() =>
            builder.WithTableEntities<SampleEntity>("Orders", "Retail", null!));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithTableInputData_NullBuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            FunctionsTestHostBuilderTablesExtensions.WithTableInputData(
                null!,
                new Dictionary<string, string>()));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithTableInputData_NullJsonMap_Throws()
    {
        var builder = new FunctionsTestHostBuilder();

        Assert.Throws<ArgumentNullException>(() =>
            builder.WithTableInputData(null!));
    }

    private static JsonElement ParseConfig(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static TableInputSyntheticBindingProvider ExtractSingleProvider(FunctionsTestHostBuilder builder)
    {
        var field = typeof(FunctionsTestHostBuilder)
            .GetField("_syntheticBindingProviders", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);

        var providers = Assert.IsType<List<ISyntheticBindingProvider>>(field.GetValue(builder));
        var provider = Assert.Single(providers);
        return Assert.IsType<TableInputSyntheticBindingProvider>(provider);
    }

    private sealed class SampleEntity
    {
        public string? PartitionKey { get; init; }
        public string? RowKey { get; init; }
        public string? PayloadValue { get; init; }
    }
}
