using System.Reflection;
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Durable;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Durable;

public class FunctionsTestHostBuilderDurableExtensionsConfigurationTests
{
    [Fact]
    public void ConfigureFakeDurableSupport_NullBuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            FunctionsTestHostBuilderDurableExtensions.ConfigureFakeDurableSupport(null!, Assembly.GetExecutingAssembly()));
    }

    [Fact]
    public void ConfigureFakeDurableSupport_NullAssembly_Throws()
    {
        var builder = new FakeBuilder();
        Assert.Throws<ArgumentNullException>(() =>
            builder.ConfigureFakeDurableSupport(null!));
    }

    [Fact]
    public void ConfigureFakeDurableSupport_RegistersCoreDurableServices()
    {
        var builder = new FakeBuilder();

        var result = builder.ConfigureFakeDurableSupport(typeof(TestFunctions).Assembly);

        Assert.Same(builder, result);
        Assert.Single(builder.SyntheticBindingProviders);
        Assert.IsType<DurableClientSyntheticBindingProvider>(builder.SyntheticBindingProviders[0]);
        Assert.Single(builder.ServiceConfigurations);

        var services = new ServiceCollection().AddLogging();
        builder.ServiceConfigurations[0](services);
        var provider = services.BuildServiceProvider();

        var durableClient = provider.GetRequiredService<DurableTaskClient>();
        Assert.IsType<FakeDurableTaskClient>(durableClient);
        Assert.IsType<FunctionsDurableClientProvider>(provider.GetRequiredService<FunctionsDurableClientProvider>());

        var converterType = typeof(Microsoft.Azure.Functions.Worker.DurableClientAttribute).Assembly.GetType(
            "Microsoft.Azure.Functions.Worker.Extensions.DurableTask.DurableTaskClientConverter",
            throwOnError: true)!;
        Assert.IsType<FakeDurableTaskClientInputConverter>(provider.GetRequiredService(converterType));

        var internalProviderType = typeof(Microsoft.Azure.Functions.Worker.DurableClientAttribute).Assembly.GetType(
            "Microsoft.Azure.Functions.Worker.Extensions.DurableTask.FunctionsDurableClientProvider",
            throwOnError: true)!;
        Assert.NotNull(provider.GetRequiredService(internalProviderType));
    }

    private sealed class FakeBuilder : IFunctionsTestHostBuilder
    {
        public List<ISyntheticBindingProvider> SyntheticBindingProviders { get; } = [];
        public List<Action<IServiceCollection>> ServiceConfigurations { get; } = [];

        public IFunctionsTestHostBuilder WithSyntheticBindingProvider(ISyntheticBindingProvider provider)
        {
            SyntheticBindingProviders.Add(provider);
            return this;
        }

        public IFunctionsTestHostBuilder ConfigureServices(Action<IServiceCollection> configure)
        {
            ServiceConfigurations.Add(configure);
            return this;
        }

        public IFunctionsTestHostBuilder WithFunctionsAssembly(Assembly assembly) => this;
        public IFunctionsTestHostBuilder WithHostBuilderFactory(Func<string[], Microsoft.Extensions.Hosting.IHostBuilder> factory) => this;
        public IFunctionsTestHostBuilder WithHostApplicationBuilderFactory(Func<string[], Microsoft.Azure.Functions.Worker.Builder.FunctionsApplicationBuilder> factory) => this;
        public IFunctionsTestHostBuilder WithLoggerFactory(Microsoft.Extensions.Logging.ILoggerFactory loggerFactory) => this;
        public IFunctionsTestHostBuilder ConfigureSetting(string key, string value) => this;
        public IFunctionsTestHostBuilder ConfigureEnvironmentVariable(string name, string value) => this;
        public IFunctionsTestHostBuilder ConfigureWorkerLogging(Action<Microsoft.Extensions.Logging.ILoggingBuilder> configure) => this;
        public IFunctionsTestHostBuilder WithInvocationTimeout(TimeSpan timeout) => this;
        public IFunctionsTestHost Build() => throw new NotSupportedException();
    }

    public static class TestFunctions
    {
        public static void Starter([Microsoft.Azure.Functions.Worker.DurableClient(TaskHub = "hub", ConnectionName = "conn")] DurableTaskClient client)
        {
        }
    }
}
