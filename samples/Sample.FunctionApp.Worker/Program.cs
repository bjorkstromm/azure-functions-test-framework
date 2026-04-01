using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Sample.FunctionApp.Worker;

await Program.CreateHostBuilder(args).Build().RunAsync();

public partial class Program
{
    // Used by FunctionsTestHostBuilder.WithHostBuilderFactory for ASP.NET Core / Kestrel mode testing.
    // Uses ConfigureFunctionsWebApplication() so the worker starts a real Kestrel server and the
    // full ASP.NET Core middleware pipeline (HttpRequest, FunctionContext, etc.) is exercised.
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWebApplication(ConfigureWorker)
            .ConfigureServices(ConfigureServices);

    // Used by FunctionsTestHostBuilder.WithHostBuilderFactory for direct gRPC mode testing.
    // Uses ConfigureFunctionsWorkerDefaults() — HTTP requests are dispatched via gRPC InvocationRequest.
    public static IHostBuilder CreateWorkerHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWorkerDefaults(ConfigureWorker)
            .ConfigureServices(ConfigureServices);

    private static void ConfigureWorker(IFunctionsWorkerApplicationBuilder workerApplication)
    {
        workerApplication.UseMiddleware<CorrelationIdMiddleware>();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ITodoService, InMemoryTodoService>();
        services.AddSingleton<IProcessedItemsService, InMemoryProcessedItemsService>();
    }
}
