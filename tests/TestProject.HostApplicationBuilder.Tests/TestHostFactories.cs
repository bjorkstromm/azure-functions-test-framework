using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace TestProject;

/// <summary>
/// Factory helpers for the main function app using FunctionsApplicationBuilder (direct gRPC mode).
/// Defined here to avoid ambiguity with the global-namespace Program classes from referenced assemblies.
/// </summary>
internal static class TestHostFactory
{
    public static FunctionsApplicationBuilder CreateApplicationBuilder(string[] args)
    {
        var builder = FunctionsApplication.CreateBuilder(args);
        ((Microsoft.Azure.Functions.Worker.IFunctionsWorkerApplicationBuilder)builder).UseMiddleware<CorrelationMiddleware>();
        builder.Services.AddSingleton<IItemService, InMemoryItemService>();
        builder.Services.AddSingleton<IProcessedItemsService, InMemoryProcessedItemsService>();
        return builder;
    }
}

/// <summary>
/// Factory helpers for the custom-route-prefix function app using FunctionsApplicationBuilder.
/// </summary>
internal static class CrpTestHostFactory
{
    public static FunctionsApplicationBuilder CreateApplicationBuilder(string[] args)
    {
        var builder = FunctionsApplication.CreateBuilder(args);
        builder.Services.AddSingleton<IItemService, InMemoryItemService>();
        return builder;
    }
}
