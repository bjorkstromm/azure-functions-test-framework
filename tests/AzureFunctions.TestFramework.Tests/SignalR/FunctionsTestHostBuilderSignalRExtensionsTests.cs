using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.SignalR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.SignalR;

/// <summary>
/// Unit tests for <see cref="FunctionsTestHostBuilderSignalRExtensions"/>.
/// </summary>
public class FunctionsTestHostBuilderSignalRExtensionsTests
{
    // ── WithSignalRConnectionInfo(url, accessToken) ───────────────────────────

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithSignalRConnectionInfo_NullBuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            FunctionsTestHostBuilderSignalRExtensions.WithSignalRConnectionInfo(
                null!, "https://example.signalr.net", "token"));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithSignalRConnectionInfo_NullUrl_Throws()
    {
        var builder = new FakeBuilder();
        Assert.Throws<ArgumentNullException>(() =>
            builder.WithSignalRConnectionInfo(null!, "token"));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithSignalRConnectionInfo_NullToken_Throws()
    {
        var builder = new FakeBuilder();
        Assert.Throws<ArgumentNullException>(() =>
            builder.WithSignalRConnectionInfo("https://example.signalr.net", null!));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithSignalRConnectionInfo_ValidArgs_RegistersProvider()
    {
        var builder = new FakeBuilder();

        var result = builder.WithSignalRConnectionInfo(
            "https://example.signalr.net", "my-token");

        Assert.Same(builder, result);
        Assert.Single(builder.RegisteredProviders);
        Assert.IsType<SignalRConnectionInfoSyntheticBindingProvider>(
            builder.RegisteredProviders[0]);
    }

    // ── WithSignalRConnectionInfo(SignalRConnectionInfo) ──────────────────────

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithSignalRConnectionInfo_NullConnectionInfo_Throws()
    {
        var builder = new FakeBuilder();
        Assert.Throws<ArgumentNullException>(() =>
            FunctionsTestHostBuilderSignalRExtensions.WithSignalRConnectionInfo(
                builder, (SignalRConnectionInfo)null!));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithSignalRConnectionInfo_ConnectionInfoObject_RegistersProvider()
    {
        var builder = new FakeBuilder();
        var connectionInfo = new SignalRConnectionInfo
        {
            Url = "https://example.signalr.net",
            AccessToken = "object-token"
        };

        var result = builder.WithSignalRConnectionInfo(connectionInfo);

        Assert.Same(builder, result);
        Assert.Single(builder.RegisteredProviders);
        Assert.IsType<SignalRConnectionInfoSyntheticBindingProvider>(
            builder.RegisteredProviders[0]);
    }

    // ── WithSignalRNegotiation ────────────────────────────────────────────────

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithSignalRNegotiation_NullBuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            FunctionsTestHostBuilderSignalRExtensions.WithSignalRNegotiation(
                null!, new SignalRNegotiationContext()));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithSignalRNegotiation_NullContext_Throws()
    {
        var builder = new FakeBuilder();
        Assert.Throws<ArgumentNullException>(() =>
            builder.WithSignalRNegotiation(null!));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithSignalRNegotiation_ValidArgs_RegistersProvider()
    {
        var builder = new FakeBuilder();
        var context = new SignalRNegotiationContext
        {
            Endpoints =
            [
                new SignalREndpointConnectionInfo { Endpoint = "https://ep.signalr.net", Name = "default" }
            ]
        };

        var result = builder.WithSignalRNegotiation(context);

        Assert.Same(builder, result);
        Assert.Single(builder.RegisteredProviders);
        Assert.IsType<SignalRNegotiationSyntheticBindingProvider>(
            builder.RegisteredProviders[0]);
    }

    // ── WithSignalREndpoints ──────────────────────────────────────────────────

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithSignalREndpoints_NullBuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            FunctionsTestHostBuilderSignalRExtensions.WithSignalREndpoints(
                null!, new SignalREndpoint { Endpoint = "https://ep.signalr.net" }));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithSignalREndpoints_NullEndpoints_Throws()
    {
        var builder = new FakeBuilder();
        Assert.Throws<ArgumentNullException>(() =>
            FunctionsTestHostBuilderSignalRExtensions.WithSignalREndpoints(builder, null!));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithSignalREndpoints_EmptyArray_RegistersProvider()
    {
        var builder = new FakeBuilder();

        var result = builder.WithSignalREndpoints();

        Assert.Same(builder, result);
        Assert.Single(builder.RegisteredProviders);
        Assert.IsType<SignalREndpointsSyntheticBindingProvider>(
            builder.RegisteredProviders[0]);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WithSignalREndpoints_WithEndpoints_RegistersProvider()
    {
        var builder = new FakeBuilder();
        var endpoint1 = new SignalREndpoint { Endpoint = "https://ep1.signalr.net", Name = "ep1" };
        var endpoint2 = new SignalREndpoint { Endpoint = "https://ep2.signalr.net", Name = "ep2" };

        var result = builder.WithSignalREndpoints(endpoint1, endpoint2);

        Assert.Same(builder, result);
        Assert.Single(builder.RegisteredProviders);
        Assert.IsType<SignalREndpointsSyntheticBindingProvider>(
            builder.RegisteredProviders[0]);
    }

    // ── FakeBuilder ───────────────────────────────────────────────────────────

    private sealed class FakeBuilder : IFunctionsTestHostBuilder
    {
        public List<ISyntheticBindingProvider> RegisteredProviders { get; } = [];

        public IFunctionsTestHostBuilder WithSyntheticBindingProvider(ISyntheticBindingProvider provider)
        {
            RegisteredProviders.Add(provider);
            return this;
        }

        // Remaining interface members — not exercised by these tests
        public IFunctionsTestHostBuilder WithFunctionsAssembly(System.Reflection.Assembly assembly) => this;
        public IFunctionsTestHostBuilder WithHostBuilderFactory(Func<string[], Microsoft.Extensions.Hosting.IHostBuilder> factory) => this;
        public IFunctionsTestHostBuilder WithHostApplicationBuilderFactory(
            Func<string[], Microsoft.Azure.Functions.Worker.Builder.FunctionsApplicationBuilder> factory) => this;
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
        public IFunctionInvoker Invoker => new FakeInvoker();
        public IServiceProvider Services => new ServiceCollection().BuildServiceProvider();
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private sealed class FakeInvoker : IFunctionInvoker
        {
            public Task<FunctionInvocationResult> InvokeAsync(
                string functionName,
                FunctionInvocationContext context,
                Func<FunctionInvocationContext, FunctionRegistration, TriggerBindingData> triggerBindingFactory,
                CancellationToken cancellationToken = default)
                => Task.FromResult(new FunctionInvocationResult { Success = true });

            public IReadOnlyDictionary<string, Microsoft.Azure.Functions.Worker.Core.FunctionMetadata.IFunctionMetadata> GetFunctions()
                => new Dictionary<string, Microsoft.Azure.Functions.Worker.Core.FunctionMetadata.IFunctionMetadata>();
        }
    }
}
