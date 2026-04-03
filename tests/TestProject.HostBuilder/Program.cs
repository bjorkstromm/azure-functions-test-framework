using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TestProject;

await Program.CreateWorkerHostBuilder(args).Build().RunAsync();

public partial class Program
{
    public static IHostBuilder CreateWorkerHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWorkerDefaults(b => b.UseMiddleware<CorrelationMiddleware>())
            .ConfigureServices(ConfigureServices);

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IItemService, InMemoryItemService>();
        services.AddSingleton<IProcessedItemsService, InMemoryProcessedItemsService>();
    }
}
