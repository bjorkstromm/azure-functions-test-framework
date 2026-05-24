using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TestProject;
using TestProject.CustomRoutePrefix;

await Program.CreateWorkerHostBuilder(args).Build().RunAsync();

/// <summary>
/// Represents this type.
/// </summary>
public partial class Program
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public static IHostBuilder CreateWorkerHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureServices(ConfigureServices);

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IItemService, InMemoryItemService>();
    }
}
