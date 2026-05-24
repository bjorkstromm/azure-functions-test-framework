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
            .ConfigureFunctionsWebApplication()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IItemService, InMemoryItemService>();
            });
}
