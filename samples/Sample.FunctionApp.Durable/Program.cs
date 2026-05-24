using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sample.FunctionApp.Durable;

await Program.CreateHostBuilder(args).Build().RunAsync();

/// <summary>
/// Represents this type.
/// </summary>
public partial class Program
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        new HostBuilder()
            .ConfigureFunctionsWebApplication()
            .ConfigureServices(services =>
            {
                services.AddSingleton<GreetingFormatter>();
            });
}
