using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TestProject;
using TestProject.CustomRoutePrefix;

await Program.CreateApplicationBuilder(args).Build().RunAsync();

/// <summary>
/// Represents this type.
/// </summary>
public partial class Program
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public static FunctionsApplicationBuilder CreateApplicationBuilder(string[] args)
    {
        var builder = FunctionsApplication.CreateBuilder(args);
        ConfigureServices(builder.Services);
        return builder;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IItemService, InMemoryItemService>();
    }
}
