using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace TestProject;

internal static class TestHostFactory
{
    public static FunctionsApplicationBuilder CreateApplicationBuilder(string[] args)
    {
        var builder = FunctionsApplication.CreateBuilder(args);
        builder.Services.AddSingleton<IItemService, InMemoryItemService>();
        return builder;
    }
}
