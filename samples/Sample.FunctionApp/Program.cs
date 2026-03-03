using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Sample.FunctionApp;

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

    // Used by FunctionsTestHostBuilder.WithHostBuilderFactory (non-WAF gRPC mode).
    // Uses ConfigureFunctionsWorkerDefaults() so that HTTP trigger invocations are
    // dispatched directly via the gRPC InvocationRequest channel without requiring a
    // running ASP.NET Core HTTP pipeline inside the worker.
    public static IHostBuilder CreateWorkerHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureServices(ConfigureServices);

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddSingleton<ITodoService, InMemoryTodoService>();
    }
}
