using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Sql;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Sql;

/// <summary>
/// Represents this type.
/// </summary>
public class FunctionsTestHostBuilderSqlExtensionsTests
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithSqlInputRows_SingleRow_RegistersProvider()
    {
        var builder = new FakeBuilder();

        var result = builder.WithSqlInputRows("select * from t", new TestRow(1));

        Assert.Same(builder, result);
        Assert.Single(builder.RegisteredProviders);
        Assert.IsType<SqlInputSyntheticBindingProvider>(builder.RegisteredProviders[0]);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithSqlInputRows_List_RegistersProvider()
    {
        var builder = new FakeBuilder();

        var result = builder.WithSqlInputRows("select * from t", (IReadOnlyList<TestRow>)[new(1), new(2)]);

        Assert.Same(builder, result);
        Assert.Single(builder.RegisteredProviders);
        Assert.IsType<SqlInputSyntheticBindingProvider>(builder.RegisteredProviders[0]);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithSqlInputJson_RegistersProvider()
    {
        var builder = new FakeBuilder();

        var result = builder.WithSqlInputJson("select * from t", """[{"id":1}]""");

        Assert.Same(builder, result);
        Assert.Single(builder.RegisteredProviders);
        Assert.IsType<SqlInputSyntheticBindingProvider>(builder.RegisteredProviders[0]);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithSqlInputRows_NullBuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            FunctionsTestHostBuilderSqlExtensions.WithSqlInputRows(null!, "select 1", new TestRow(1)));
    }

    private sealed record TestRow(int Id);

    private sealed class FakeBuilder : IFunctionsTestHostBuilder
    {
        public List<ISyntheticBindingProvider> RegisteredProviders { get; } = [];

        public IFunctionsTestHostBuilder WithSyntheticBindingProvider(ISyntheticBindingProvider provider)
        {
            RegisteredProviders.Add(provider);
            return this;
        }

        public IFunctionsTestHostBuilder WithFunctionsAssembly(System.Reflection.Assembly assembly) => this;
        public IFunctionsTestHostBuilder WithHostBuilderFactory(Func<string[], Microsoft.Extensions.Hosting.IHostBuilder> factory) => this;
        public IFunctionsTestHostBuilder WithHostApplicationBuilderFactory(Func<string[], Microsoft.Azure.Functions.Worker.Builder.FunctionsApplicationBuilder> factory) => this;
        public IFunctionsTestHostBuilder WithLoggerFactory(Microsoft.Extensions.Logging.ILoggerFactory loggerFactory) => this;
        public IFunctionsTestHostBuilder ConfigureServices(Action<IServiceCollection> configure) => this;
        public IFunctionsTestHostBuilder ConfigureSetting(string key, string value) => this;
        public IFunctionsTestHostBuilder ConfigureEnvironmentVariable(string name, string value) => this;
        public IFunctionsTestHostBuilder ConfigureWorkerLogging(Action<Microsoft.Extensions.Logging.ILoggingBuilder> configure) => this;
        public IFunctionsTestHostBuilder WithInvocationTimeout(TimeSpan timeout) => this;
        public IFunctionsTestHost Build() => new FakeHost();
    }

    private sealed class FakeHost : IFunctionsTestHost
    {
        public IFunctionInvoker Invoker => null!;
        public IServiceProvider Services => new ServiceCollection().BuildServiceProvider();
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
