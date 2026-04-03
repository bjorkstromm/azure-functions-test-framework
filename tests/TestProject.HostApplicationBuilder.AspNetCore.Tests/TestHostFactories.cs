using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace TestProject;

/// <summary>
/// Factory helpers for the main function app using FunctionsApplicationBuilder.
/// Defined here to avoid ambiguity with the global-namespace Program classes from referenced assemblies.
/// </summary>
internal static class TestHostFactory
{
    /// <summary>Creates a builder using ConfigureFunctionsWebApplication (ASP.NET Core / Kestrel mode).</summary>
    public static FunctionsApplicationBuilder CreateWebApplicationBuilder(string[] args)
    {
        var builder = FunctionsApplication.CreateBuilder(args);
        builder.UseMiddleware<CorrelationMiddleware>();
        builder.ConfigureFunctionsWebApplication();
        builder.Services.AddSingleton<IItemService, InMemoryItemService>();
        builder.Services.AddSingleton<IProcessedItemsService, InMemoryProcessedItemsService>();
        return builder;
    }

    /// <summary>Creates a builder without ConfigureFunctionsWebApplication (direct gRPC mode).</summary>
    public static FunctionsApplicationBuilder CreateApplicationBuilder(string[] args)
    {
        var builder = FunctionsApplication.CreateBuilder(args);
        builder.UseMiddleware<CorrelationMiddleware>();
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
    /// <summary>Creates a builder using ConfigureFunctionsWebApplication (ASP.NET Core / Kestrel mode).</summary>
    public static FunctionsApplicationBuilder CreateWebApplicationBuilder(string[] args)
    {
        var builder = FunctionsApplication.CreateBuilder(args);
        builder.ConfigureFunctionsWebApplication();
        builder.Services.AddSingleton<IItemService, InMemoryItemService>();
        return builder;
    }

    /// <summary>Creates a builder without ConfigureFunctionsWebApplication (direct gRPC mode).</summary>
    public static FunctionsApplicationBuilder CreateApplicationBuilder(string[] args)
    {
        var builder = FunctionsApplication.CreateBuilder(args);
        builder.Services.AddSingleton<IItemService, InMemoryItemService>();
        return builder;
    }
}
