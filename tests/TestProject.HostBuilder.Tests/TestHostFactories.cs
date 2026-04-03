using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TestProject;

/// <summary>
/// Factory helpers for the main function app using IHostBuilder + ConfigureFunctionsWorkerDefaults.
/// Defined here to avoid ambiguity with the global-namespace Program classes from referenced assemblies.
/// </summary>
internal static class TestHostFactory
{
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
/// Factory helpers for the custom-route-prefix function app using IHostBuilder + ConfigureFunctionsWorkerDefaults.
/// </summary>
internal static class CrpTestHostFactory
{
    public static IHostBuilder CreateWorkerHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IItemService, InMemoryItemService>();
            });
}
