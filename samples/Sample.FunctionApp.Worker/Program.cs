using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Sample.FunctionApp.Worker;

await Program.CreateHostBuilder(args).Build().RunAsync();

public partial class Program
{
    // ── IHostBuilder factories ────────────────────────────────────────────────

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

    // ── IHostApplicationBuilder factories ────────────────────────────────────

    // Used by FunctionsTestHostBuilder.WithHostApplicationBuilderFactory for ASP.NET Core / Kestrel
    // mode testing. FunctionsApplication.CreateBuilder() sets up worker defaults; calling
    // ConfigureFunctionsWebApplication() adds the ASP.NET Core integration so the full Kestrel
    // pipeline (HttpRequest, FunctionContext, typed route params, CancellationToken) is exercised.
    public static FunctionsApplicationBuilder CreateHostApplicationBuilder(string[] args)
    {
        var builder = FunctionsApplication.CreateBuilder(args);
        ConfigureWorker(builder);
        builder.ConfigureFunctionsWebApplication();
        ConfigureServices(builder.Services);
        return builder;
    }

    // Used by FunctionsTestHostBuilder.WithHostApplicationBuilderFactory for direct gRPC mode
    // testing. FunctionsApplication.CreateBuilder() sets up worker defaults and the builder
    // implements IFunctionsWorkerApplicationBuilder so middleware can be registered directly.
    public static FunctionsApplicationBuilder CreateWorkerHostApplicationBuilder(string[] args)
    {
        var builder = FunctionsApplication.CreateBuilder(args);
        ConfigureWorker(builder);
        ConfigureServices(builder.Services);
        return builder;
    }

    // ── Shared configuration helpers ─────────────────────────────────────────

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
