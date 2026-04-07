using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TestProject;

await Program.CreateHostBuilder(args).Build().RunAsync();

public partial class Program
{
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWebApplication()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IItemService, InMemoryItemService>();
            });
}
