using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AzureFunctions.TestFramework.Core;

/// <summary>
/// Builder for configuring and creating a <see cref="IFunctionsTestHost"/>.
/// Provides a fluent API similar to WebApplicationFactory.
/// </summary>
public interface IFunctionsTestHostBuilder
{
    /// <summary>
    /// Configures services in the Functions worker's dependency injection container.
    /// </summary>
    IFunctionsTestHostBuilder ConfigureServices(Action<IServiceCollection> configure);

    /// <summary>
    /// Specifies the assembly containing the Functions to test.
    /// </summary>
    IFunctionsTestHostBuilder WithFunctionsAssembly(Assembly assembly);

    /// <summary>
    /// Configures a configuration value that will be added to the worker host's configuration.
    /// Supports simple keys and hierarchical keys such as <c>Section:Key</c>.
    /// </summary>
    IFunctionsTestHostBuilder ConfigureSetting(string key, string value);

    /// <summary>
    /// Sets a process-level environment variable before the worker starts, making it visible
    /// to the worker's <c>IConfiguration</c> (via <c>AddEnvironmentVariables()</c>).
    /// <para>
    /// Use this to simulate Azure Functions App Settings that your function reads via
    /// <c>Environment.GetEnvironmentVariable()</c> or <c>IConfiguration[key]</c>.
    /// </para>
    /// <para>
    /// <b>Note:</b> Environment variables are process-wide and the framework does <b>not</b>
    /// restore their previous values after the test completes.  Variables set here persist for
    /// the lifetime of the test process and will be visible to all subsequently created hosts.
    /// Tests that rely on different values for the same variable should run sequentially
    /// (separate xUnit test collection).  Prefer <see cref="ConfigureSetting"/> for values
    /// that can be expressed as in-memory configuration overrides.
    /// </para>
    /// </summary>
    /// <param name="name">The environment variable name.</param>
    /// <param name="value">The environment variable value.</param>
    IFunctionsTestHostBuilder ConfigureEnvironmentVariable(string name, string value);

    /// <summary>
    /// Specifies a factory used to create the underlying <see cref="IHostBuilder"/> for the
    /// Functions worker.  Pass your application's <c>Program.CreateHostBuilder</c> or
    /// <c>Program.CreateWorkerHostBuilder</c> method here so that all services, middleware and
    /// configuration registered in <c>Program.cs</c> are automatically available in the test host
    /// — exactly as they would be at runtime.
    /// <para>
    /// Both <c>ConfigureFunctionsWorkerDefaults()</c> and <c>ConfigureFunctionsWebApplication()</c>
    /// are supported:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     <c>ConfigureFunctionsWorkerDefaults()</c> — invocations are dispatched directly via
    ///     the gRPC <c>InvocationRequest</c> channel (no ASP.NET Core HTTP pipeline needed).
    ///   </description></item>
    ///   <item><description>
    ///     <c>ConfigureFunctionsWebApplication()</c> — the worker's ASP.NET Core HTTP server is
    ///     started on an ephemeral port; <see cref="IFunctionsTestHost.CreateHttpClient"/> returns
    ///     a client that forwards requests to that server.
    ///   </description></item>
    /// </list>
    /// <para>
    /// When a factory is provided the framework overlays the gRPC connection settings and
    /// <c>IAutoConfigureStartup</c> registrations on top of the returned builder; any
    /// <see cref="ConfigureServices"/> calls are applied afterwards and may override services
    /// already registered by the factory.
    /// </para>
    /// </summary>
    /// <param name="factory">
    /// A delegate that accepts command-line arguments and returns a configured
    /// <see cref="IHostBuilder"/>, e.g. <c>args => Program.CreateHostBuilder(args)</c>.
    /// </param>
    IFunctionsTestHostBuilder WithHostBuilderFactory(Func<string[], IHostBuilder> factory);

    /// <summary>
    /// Specifies a factory used to create the underlying <see cref="Microsoft.Azure.Functions.Worker.Builder.FunctionsApplicationBuilder"/>
    /// for the Functions worker.  Pass your application's <c>Program.CreateHostApplicationBuilder</c> or
    /// <c>Program.CreateWorkerHostApplicationBuilder</c> method here so that all services, middleware and
    /// configuration registered in <c>Program.cs</c> are automatically available in the test host
    /// — exactly as they would be at runtime.
    /// <para>
    /// Use <c>FunctionsApplication.CreateBuilder(args)</c> inside the factory, which sets up
    /// the worker defaults (<c>ConfigureFunctionsWorkerDefaults</c>) automatically:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     Without <c>ConfigureFunctionsWebApplication()</c> — invocations are dispatched directly
    ///     via the gRPC <c>InvocationRequest</c> channel (direct gRPC mode).
    ///   </description></item>
    ///   <item><description>
    ///     With <c>ConfigureFunctionsWebApplication()</c> — the worker's ASP.NET Core HTTP server is
    ///     started on an ephemeral port; <see cref="IFunctionsTestHost.CreateHttpClient"/> returns
    ///     a client that forwards requests to that server (ASP.NET Core / Kestrel mode).
    ///   </description></item>
    /// </list>
    /// <para>
    /// When a factory is provided the framework overlays the gRPC connection settings and
    /// <c>IAutoConfigureStartup</c> registrations on top of the returned builder; any
    /// <see cref="ConfigureServices"/> calls are applied afterwards and may override services
    /// already registered by the factory.
    /// </para>
    /// </summary>
    /// <param name="factory">
    /// A delegate that accepts command-line arguments and returns a configured
    /// <see cref="Microsoft.Azure.Functions.Worker.Builder.FunctionsApplicationBuilder"/>,
    /// e.g. <c>args => Program.CreateHostApplicationBuilder(args)</c>.
    /// </param>
    IFunctionsTestHostBuilder WithHostApplicationBuilderFactory(
        Func<string[], Microsoft.Azure.Functions.Worker.Builder.FunctionsApplicationBuilder> factory);

    /// <summary>
    /// Overrides the <see cref="Microsoft.Extensions.Logging.ILoggerFactory"/> used by the test
    /// host infrastructure (gRPC server, worker host service, etc.).
    /// <para>
    /// By default the framework creates a factory that writes to the console.  Supply a custom
    /// factory here to route framework logs to your test output — for example xUnit's
    /// <c>ITestOutputHelper</c>, Serilog, or any other <c>ILoggerProvider</c>.
    /// </para>
    /// </summary>
    /// <param name="loggerFactory">The logger factory to use.</param>
    IFunctionsTestHostBuilder WithLoggerFactory(Microsoft.Extensions.Logging.ILoggerFactory loggerFactory);

    /// <summary>
    /// Builds and returns the configured test host.
    /// </summary>
    IFunctionsTestHost Build();
}
