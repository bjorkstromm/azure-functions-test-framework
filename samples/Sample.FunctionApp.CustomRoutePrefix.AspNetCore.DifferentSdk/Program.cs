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
    // Used by FunctionsWebApplicationFactory (WAF / ASP.NET Core integration mode).
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWebApplication()
            .ConfigureServices(ConfigureServices);

    // Used by FunctionsTestHost (non-WAF / ASP.NET Core integration mode).
    // Returns a HostBuilder that uses ConfigureFunctionsWebApplication() so the test host
    // can start a real Kestrel server and forward HTTP requests through the ASP.NET Core pipeline.
    public static IHostBuilder CreateWorkerHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWebApplication()
            .ConfigureServices(ConfigureServices);

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IProductService, InMemoryProductService>();
    }
}
