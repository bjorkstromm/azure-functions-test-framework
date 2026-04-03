using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sample.FunctionApp;

await new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services => services.AddSingleton<ITodoService, InMemoryTodoService>())
    .Build()
    .RunAsync();
