using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TestProject;

await Program.CreateHostBuilder(args).Build().RunAsync();

/// <summary>
/// Represents this type.
/// </summary>
public partial class Program
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWebApplication(b => b.UseMiddleware<CorrelationMiddleware>())
            .ConfigureServices(services =>
            {
                services.AddSingleton<IItemService, InMemoryItemService>();
                services.AddSingleton<IProcessedItemsService, InMemoryProcessedItemsService>();
            });
}
