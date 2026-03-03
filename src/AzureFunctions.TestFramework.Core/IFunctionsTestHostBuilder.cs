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
    /// Specifies the Functions worker assembly type.
    /// </summary>
    IFunctionsTestHostBuilder WithFunctionsWorkerAssembly(Assembly assembly);

    /// <summary>
    /// Configures additional settings.
    /// </summary>
    IFunctionsTestHostBuilder ConfigureSetting(string key, string value);

    /// <summary>
    /// Specifies a factory used to create the underlying <see cref="IHostBuilder"/> for the
    /// Functions worker.  Pass your application's <c>Program.CreateWorkerHostBuilder</c> method
    /// here so that all services, middleware and configuration registered in <c>Program.cs</c>
    /// are automatically available in the test host — exactly as they would be at runtime.
    /// <para>
    /// The factory must configure the worker with <c>ConfigureFunctionsWorkerDefaults()</c>
    /// (not <c>ConfigureFunctionsWebApplication()</c>), because the non-WAF gRPC path dispatches
    /// invocations directly over gRPC and does not use an ASP.NET Core HTTP pipeline inside the
    /// worker.
    /// </para>
    /// <para>
    /// When a factory is provided the framework overlays the gRPC connection settings and
    /// <c>IAutoConfigureStartup</c> registrations on top of the returned builder; any
    /// <see cref="ConfigureServices"/> calls are applied afterwards and may override services
    /// already registered by the factory.
    /// </para>
    /// </summary>
    /// <param name="factory">
    /// A delegate that accepts command-line arguments and returns a configured
    /// <see cref="IHostBuilder"/>, e.g. <c>args => Program.CreateWorkerHostBuilder(args)</c>.
    /// </param>
    IFunctionsTestHostBuilder WithHostBuilderFactory(Func<string[], IHostBuilder> factory);

    /// <summary>
    /// Builds and returns the configured test host.
    /// </summary>
    IFunctionsTestHost Build();
}
