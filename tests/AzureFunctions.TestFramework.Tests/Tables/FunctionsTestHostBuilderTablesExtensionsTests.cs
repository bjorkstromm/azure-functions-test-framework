using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Tables;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Tables;

/// <summary>
/// Unit tests for <see cref="FunctionsTestHostBuilderTablesExtensions"/>.
/// </summary>
public class FunctionsTestHostBuilderTablesExtensionsTests
{
    // ── WithTableEntity ────────────────────────────────────────────────────────

    [Fact]
    public void WithTableEntity_RegistersProviderWithCorrectKey()
    {
        var builder = new FakeBuilder();
        var entity = new TestEntity { Name = "Widget", Quantity = 5 };

        builder.WithTableEntity("Products", "Electronics", "item-1", entity);

        Assert.Single(builder.RegisteredProviders);
        var provider = builder.RegisteredProviders[0];
        Assert.Equal("table", provider.BindingType);

        var config = ParseConfig("""{"direction":"In","tableName":"Products","partitionKey":"Electronics","rowKey":"item-1"}""");
        var result = provider.CreateSyntheticParameter("e", config);

        using var doc = JsonDocument.Parse(result!.Json!);
        Assert.Equal("Widget", doc.RootElement.GetProperty("Name").GetString());
        Assert.Equal(5, doc.RootElement.GetProperty("Quantity").GetInt32());
    }

    [Fact]
    public void WithTableEntity_ReturnsSameBuilderForChaining()
    {
        var builder = new FakeBuilder();
        var result = builder.WithTableEntity("T", "PK", "RK", new TestEntity { Name = "x" });
        Assert.Same(builder, result);
    }

    [Fact]
    public void WithTableEntity_NullBuilder_Throws()
    {
        IFunctionsTestHostBuilder builder = null!;
        Assert.Throws<ArgumentNullException>(() =>
            builder.WithTableEntity("T", "PK", "RK", new TestEntity()));
    }

    [Fact]
    public void WithTableEntity_NullTableName_Throws()
    {
        var builder = new FakeBuilder();
        Assert.Throws<ArgumentNullException>(() =>
            builder.WithTableEntity(null!, "PK", "RK", new TestEntity()));
    }

    [Fact]
    public void WithTableEntity_EmptyTableName_Throws()
    {
        var builder = new FakeBuilder();
        Assert.Throws<ArgumentException>(() =>
            builder.WithTableEntity("", "PK", "RK", new TestEntity()));
    }

    [Fact]
    public void WithTableEntity_NullPartitionKey_Throws()
    {
        var builder = new FakeBuilder();
        Assert.Throws<ArgumentNullException>(() =>
            builder.WithTableEntity("T", null!, "RK", new TestEntity()));
    }

    [Fact]
    public void WithTableEntity_EmptyPartitionKey_Throws()
    {
        var builder = new FakeBuilder();
        Assert.Throws<ArgumentException>(() =>
            builder.WithTableEntity("T", "", "RK", new TestEntity()));
    }

    [Fact]
    public void WithTableEntity_NullRowKey_Throws()
    {
        var builder = new FakeBuilder();
        Assert.Throws<ArgumentNullException>(() =>
            builder.WithTableEntity("T", "PK", null!, new TestEntity()));
    }

    [Fact]
    public void WithTableEntity_EmptyRowKey_Throws()
    {
        var builder = new FakeBuilder();
        Assert.Throws<ArgumentException>(() =>
            builder.WithTableEntity("T", "PK", "", new TestEntity()));
    }

    [Fact]
    public void WithTableEntity_NullEntity_Throws()
    {
        var builder = new FakeBuilder();
        Assert.Throws<ArgumentNullException>(() =>
            builder.WithTableEntity<TestEntity>("T", "PK", "RK", null!));
    }

    // ── WithTableEntities (table-scope, no partition key) ─────────────────────

    [Fact]
    public void WithTableEntities_TableScope_RegistersProviderWithTableKey()
    {
        var builder = new FakeBuilder();
        var entities = new[] { new TestEntity { Name = "A" }, new TestEntity { Name = "B" } };

        builder.WithTableEntities("Inventory", entities);

        Assert.Single(builder.RegisteredProviders);
        var provider = builder.RegisteredProviders[0];

        var config = ParseConfig("""{"direction":"In","tableName":"Inventory"}""");
        var result = provider.CreateSyntheticParameter("list", config);

        using var doc = JsonDocument.Parse(result!.Json!);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(2, doc.RootElement.GetArrayLength());
        Assert.Equal("A", doc.RootElement[0].GetProperty("Name").GetString());
        Assert.Equal("B", doc.RootElement[1].GetProperty("Name").GetString());
    }

    [Fact]
    public void WithTableEntities_TableScope_ReturnsSameBuilderForChaining()
    {
        var builder = new FakeBuilder();
        var result = builder.WithTableEntities("T", new[] { new TestEntity() });
        Assert.Same(builder, result);
    }

    [Fact]
    public void WithTableEntities_TableScope_NullBuilder_Throws()
    {
        IFunctionsTestHostBuilder builder = null!;
        Assert.Throws<ArgumentNullException>(() =>
            builder.WithTableEntities("T", new[] { new TestEntity() }));
    }

    [Fact]
    public void WithTableEntities_TableScope_NullTableName_Throws()
    {
        var builder = new FakeBuilder();
        Assert.Throws<ArgumentNullException>(() =>
            builder.WithTableEntities(null!, new[] { new TestEntity() }));
    }

    [Fact]
    public void WithTableEntities_TableScope_EmptyTableName_Throws()
    {
        var builder = new FakeBuilder();
        Assert.Throws<ArgumentException>(() =>
            builder.WithTableEntities("", new[] { new TestEntity() }));
    }

    [Fact]
    public void WithTableEntities_TableScope_NullEntities_Throws()
    {
        var builder = new FakeBuilder();
        Assert.Throws<ArgumentNullException>(() =>
            builder.WithTableEntities<TestEntity>("T", (IEnumerable<TestEntity>)null!));
    }

    // ── WithTableEntities (partition-scope) ───────────────────────────────────

    [Fact]
    public void WithTableEntities_PartitionScope_RegistersProviderWithPartitionKey()
    {
        var builder = new FakeBuilder();
        var entities = new[] { new TestEntity { Name = "X" } };

        builder.WithTableEntities("Orders", "Europe", entities);

        Assert.Single(builder.RegisteredProviders);
        var provider = builder.RegisteredProviders[0];

        var config = ParseConfig("""{"direction":"In","tableName":"Orders","partitionKey":"Europe"}""");
        var result = provider.CreateSyntheticParameter("list", config);

        using var doc = JsonDocument.Parse(result!.Json!);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(1, doc.RootElement.GetArrayLength());
        Assert.Equal("X", doc.RootElement[0].GetProperty("Name").GetString());
    }

    [Fact]
    public void WithTableEntities_PartitionScope_ReturnsSameBuilderForChaining()
    {
        var builder = new FakeBuilder();
        var result = builder.WithTableEntities("T", "PK", new[] { new TestEntity() });
        Assert.Same(builder, result);
    }

    [Fact]
    public void WithTableEntities_PartitionScope_NullBuilder_Throws()
    {
        IFunctionsTestHostBuilder builder = null!;
        Assert.Throws<ArgumentNullException>(() =>
            builder.WithTableEntities("T", "PK", new[] { new TestEntity() }));
    }

    [Fact]
    public void WithTableEntities_PartitionScope_NullTableName_Throws()
    {
        var builder = new FakeBuilder();
        Assert.Throws<ArgumentNullException>(() =>
            builder.WithTableEntities(null!, "PK", new[] { new TestEntity() }));
    }

    [Fact]
    public void WithTableEntities_PartitionScope_EmptyTableName_Throws()
    {
        var builder = new FakeBuilder();
        Assert.Throws<ArgumentException>(() =>
            builder.WithTableEntities("", "PK", new[] { new TestEntity() }));
    }

    [Fact]
    public void WithTableEntities_PartitionScope_NullPartitionKey_Throws()
    {
        var builder = new FakeBuilder();
        Assert.Throws<ArgumentNullException>(() =>
            builder.WithTableEntities("T", null!, new[] { new TestEntity() }));
    }

    [Fact]
    public void WithTableEntities_PartitionScope_EmptyPartitionKey_Throws()
    {
        var builder = new FakeBuilder();
        Assert.Throws<ArgumentException>(() =>
            builder.WithTableEntities("T", "", new[] { new TestEntity() }));
    }

    [Fact]
    public void WithTableEntities_PartitionScope_NullEntities_Throws()
    {
        var builder = new FakeBuilder();
        Assert.Throws<ArgumentNullException>(() =>
            builder.WithTableEntities<TestEntity>("T", "PK", (IEnumerable<TestEntity>)null!));
    }

    // ── WithTableInputData ─────────────────────────────────────────────────────

    [Fact]
    public void WithTableInputData_RegistersProviderWithSuppliedDictionary()
    {
        var builder = new FakeBuilder();
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["T/PK/RK"] = """{"Id":42}"""
        };

        builder.WithTableInputData(dict);

        Assert.Single(builder.RegisteredProviders);
        var provider = builder.RegisteredProviders[0];

        var config = ParseConfig("""{"direction":"In","tableName":"T","partitionKey":"PK","rowKey":"RK"}""");
        var result = provider.CreateSyntheticParameter("e", config);

        using var doc = JsonDocument.Parse(result!.Json!);
        Assert.Equal(42, doc.RootElement.GetProperty("Id").GetInt32());
    }

    [Fact]
    public void WithTableInputData_ReturnsSameBuilderForChaining()
    {
        var builder = new FakeBuilder();
        var result = builder.WithTableInputData(new Dictionary<string, string>());
        Assert.Same(builder, result);
    }

    [Fact]
    public void WithTableInputData_NullBuilder_Throws()
    {
        IFunctionsTestHostBuilder builder = null!;
        Assert.Throws<ArgumentNullException>(() =>
            builder.WithTableInputData(new Dictionary<string, string>()));
    }

    [Fact]
    public void WithTableInputData_NullDictionary_Throws()
    {
        var builder = new FakeBuilder();
        Assert.Throws<ArgumentNullException>(() =>
            builder.WithTableInputData(null!));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static JsonElement ParseConfig(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private sealed class TestEntity
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    /// <summary>
    /// Minimal <see cref="IFunctionsTestHostBuilder"/> stub that captures
    /// registered <see cref="ISyntheticBindingProvider"/> instances.
    /// </summary>
    private sealed class FakeBuilder : IFunctionsTestHostBuilder
    {
        public List<ISyntheticBindingProvider> RegisteredProviders { get; } = new();

        public IFunctionsTestHostBuilder WithSyntheticBindingProvider(ISyntheticBindingProvider provider)
        {
            RegisteredProviders.Add(provider);
            return this;
        }

        public IFunctionsTestHostBuilder ConfigureServices(Action<IServiceCollection> configure) => this;
        public IFunctionsTestHostBuilder WithFunctionsAssembly(Assembly assembly) => this;
        public IFunctionsTestHostBuilder ConfigureSetting(string key, string value) => this;
        public IFunctionsTestHostBuilder ConfigureEnvironmentVariable(string name, string value) => this;
        public IFunctionsTestHostBuilder WithHostBuilderFactory(Func<string[], IHostBuilder> factory) => this;
        public IFunctionsTestHostBuilder WithHostApplicationBuilderFactory(Func<string[], Microsoft.Azure.Functions.Worker.Builder.FunctionsApplicationBuilder> factory) => this;
        public IFunctionsTestHostBuilder WithLoggerFactory(ILoggerFactory loggerFactory) => this;
        public IFunctionsTestHostBuilder ConfigureWorkerLogging(Action<ILoggingBuilder> configure) => this;
        public IFunctionsTestHostBuilder WithInvocationTimeout(TimeSpan timeout) => this;
        public IFunctionsTestHost Build() => throw new NotSupportedException();
    }
}
