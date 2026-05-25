using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Sample.FunctionApp.Worker;

await Program.CreateHostBuilder(args).Build().RunAsync();

public partial class Program
{
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWebApplication(worker => worker.UseMiddleware<CorrelationIdMiddleware>())
            .ConfigureServices(services =>
            {
                services.AddSingleton<ITodoService, InMemoryTodoService>();
                services.AddSingleton<IProcessedItemsService, InMemoryProcessedItemsService>();
            });
}
