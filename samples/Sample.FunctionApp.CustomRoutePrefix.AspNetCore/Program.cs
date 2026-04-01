using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Sample.FunctionApp.CustomRoutePrefix.AspNetCore;

await Program.CreateHostBuilder(args).Build().RunAsync();

// Expose Program for WebApplicationFactory-based testing.
// CreateHostBuilder must be public static so that WebApplicationFactory (via HostFactoryResolver)
// can locate and call it to build the host with a TestServer instead of Kestrel.
public partial class Program
{
    // Used by FunctionsTestHostBuilder.WithHostBuilderFactory for ASP.NET Core / Kestrel mode testing.
    // Uses ConfigureFunctionsWebApplication() so the worker starts a real Kestrel server and the
    // full ASP.NET Core middleware pipeline (HttpRequest, FunctionContext, Guid route params, etc.)
    // is exercised end-to-end.
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWebApplication()
            .ConfigureServices(ConfigureServices);

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IProductService, InMemoryProductService>();
    }
}
