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
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWebApplication()
            .ConfigureServices(services =>
            {
                services.AddApplicationInsightsTelemetryWorkerService();
                services.ConfigureFunctionsApplicationInsights();
                services.AddSingleton<ITodoService, InMemoryTodoService>();
            });
}
