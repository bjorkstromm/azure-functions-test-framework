using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Sample.FunctionApp.CustomRoutePrefix;

await Program.CreateWorkerHostBuilder(args).Build().RunAsync();

public partial class Program
{
    // Used by FunctionsTestHostBuilder.WithHostBuilderFactory (gRPC-direct mode).
    public static IHostBuilder CreateWorkerHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureServices(ConfigureServices);

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IProductService, InMemoryProductService>();
    }
}
