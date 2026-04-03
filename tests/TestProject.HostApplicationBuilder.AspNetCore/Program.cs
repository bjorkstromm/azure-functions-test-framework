using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TestProject;

await Program.CreateWebApplicationBuilder(args).Build().RunAsync();

public partial class Program
{
    public static FunctionsApplicationBuilder CreateWebApplicationBuilder(string[] args)
    {
        var builder = FunctionsApplication.CreateBuilder(args);
        ((Microsoft.Azure.Functions.Worker.IFunctionsWorkerApplicationBuilder)builder).UseMiddleware<CorrelationMiddleware>();
        builder.ConfigureFunctionsWebApplication();
        ConfigureServices(builder.Services);
        return builder;
    }

    public static FunctionsApplicationBuilder CreateApplicationBuilder(string[] args)
    {
        var builder = FunctionsApplication.CreateBuilder(args);
        ((Microsoft.Azure.Functions.Worker.IFunctionsWorkerApplicationBuilder)builder).UseMiddleware<CorrelationMiddleware>();
        ConfigureServices(builder.Services);
        return builder;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IItemService, InMemoryItemService>();
        services.AddSingleton<IProcessedItemsService, InMemoryProcessedItemsService>();
    }
}
