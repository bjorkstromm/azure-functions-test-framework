using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sample.FunctionApp.Durable;

await Program.CreateHostBuilder(args).Build().RunAsync();

public partial class Program
{
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWebApplication()
            .ConfigureServices(services =>
            {
                services.AddSingleton<GreetingFormatter>();
            });
}
