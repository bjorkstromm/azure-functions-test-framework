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
        // ConfigureFunctionsWebApplication must be called BEFORE UseMiddleware so that
        // FunctionsHttpProxyingMiddleware runs first and sets Items["HttpRequestContext"]
        // before any user middleware that may call GetHttpRequestDataAsync().
        builder.ConfigureFunctionsWebApplication();
        builder.UseMiddleware<CorrelationMiddleware>();
        ConfigureServices(builder.Services);
        return builder;
    }

    public static FunctionsApplicationBuilder CreateApplicationBuilder(string[] args)
    {
        var builder = FunctionsApplication.CreateBuilder(args);
        builder.UseMiddleware<CorrelationMiddleware>();
        ConfigureServices(builder.Services);
        return builder;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IItemService, InMemoryItemService>();
        services.AddSingleton<IProcessedItemsService, InMemoryProcessedItemsService>();
    }
}
