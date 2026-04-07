using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        builder.UseMiddleware<CorrelationMiddleware>();
        builder.Services.AddSingleton<IItemService, InMemoryItemService>();
        builder.Services.AddSingleton<IProcessedItemsService, InMemoryProcessedItemsService>();
        return builder;
    }
}
