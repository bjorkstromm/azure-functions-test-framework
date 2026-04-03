using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TestProject;

/// <summary>
/// Factory helpers for the main function app using IHostBuilder.
/// Defined here to avoid ambiguity with the global-namespace Program classes from referenced assemblies.
/// </summary>
internal static class TestHostFactory
{
    /// <summary>Creates a host builder using ConfigureFunctionsWebApplication (ASP.NET Core / Kestrel mode).</summary>
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWebApplication(b => b.UseMiddleware<CorrelationMiddleware>())
            .ConfigureServices(services =>
            {
                services.AddSingleton<IItemService, InMemoryItemService>();
                services.AddSingleton<IProcessedItemsService, InMemoryProcessedItemsService>();
            });

    /// <summary>Creates a host builder using ConfigureFunctionsWorkerDefaults (direct gRPC mode).</summary>
    public static IHostBuilder CreateWorkerHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWorkerDefaults(b => b.UseMiddleware<CorrelationMiddleware>())
            .ConfigureServices(services =>
            {
                services.AddSingleton<IItemService, InMemoryItemService>();
                services.AddSingleton<IProcessedItemsService, InMemoryProcessedItemsService>();
            });
}

/// <summary>
/// Factory helpers for the custom-route-prefix function app using IHostBuilder.
/// </summary>
internal static class CrpTestHostFactory
{
    /// <summary>Creates a host builder using ConfigureFunctionsWebApplication (ASP.NET Core / Kestrel mode).</summary>
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWebApplication()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IItemService, InMemoryItemService>();
            });

    /// <summary>Creates a host builder using ConfigureFunctionsWorkerDefaults (direct gRPC mode).</summary>
    public static IHostBuilder CreateWorkerHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IItemService, InMemoryItemService>();
            });
}
