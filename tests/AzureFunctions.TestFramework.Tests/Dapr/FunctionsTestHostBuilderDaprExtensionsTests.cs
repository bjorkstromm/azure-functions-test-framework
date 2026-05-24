using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Dapr;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Dapr;

/// <summary>
/// Represents this type.
/// </summary>
public class FunctionsTestHostBuilderDaprExtensionsTests
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithDaprStateInput_ValidArgs_RegistersProvider()
    {
        var builder = new FakeBuilder();

        var result = builder.WithDaprStateInput("store", "key", "value");

        Assert.Same(builder, result);
        Assert.Single(builder.RegisteredProviders);
        Assert.IsType<DaprStateInputSyntheticBindingProvider>(builder.RegisteredProviders[0]);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithDaprStateInputJson_ValidArgs_RegistersProvider()
    {
        var builder = new FakeBuilder();

        var result = builder.WithDaprStateInputJson("store", "key", """{"x":1}""");

        Assert.Same(builder, result);
        Assert.Single(builder.RegisteredProviders);
        Assert.IsType<DaprStateInputSyntheticBindingProvider>(builder.RegisteredProviders[0]);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithDaprSecretInput_ValidArgs_RegistersProvider()
    {
        var builder = new FakeBuilder();

        var result = builder.WithDaprSecretInput("secrets", "key", "value");

        Assert.Same(builder, result);
        Assert.Single(builder.RegisteredProviders);
        Assert.IsType<DaprSecretInputSyntheticBindingProvider>(builder.RegisteredProviders[0]);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithDaprSecretInputJson_ValidArgs_RegistersProvider()
    {
        var builder = new FakeBuilder();

        var result = builder.WithDaprSecretInputJson("secrets", "key", """{"x":1}""");

        Assert.Same(builder, result);
        Assert.Single(builder.RegisteredProviders);
        Assert.IsType<DaprSecretInputSyntheticBindingProvider>(builder.RegisteredProviders[0]);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithDaprStateInput_NullBuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            FunctionsTestHostBuilderDaprExtensions.WithDaprStateInput(null!, "store", "key", "value"));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithDaprSecretInput_NullBuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            FunctionsTestHostBuilderDaprExtensions.WithDaprSecretInput(null!, "secrets", "key", "value"));
    }

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
