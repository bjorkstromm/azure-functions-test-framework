using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

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
    /// Builds and returns the configured test host.
    /// </summary>
    IFunctionsTestHost Build();
}
