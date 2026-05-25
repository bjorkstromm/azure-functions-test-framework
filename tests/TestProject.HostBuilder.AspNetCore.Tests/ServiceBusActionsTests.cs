using AzureFunctions.TestFramework.ServiceBus;
using Microsoft.Extensions.DependencyInjection;

namespace TestProject;

public class ServiceBusActionsTests : ServiceBusActionsTestsBase
{
    public ServiceBusActionsTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostWithServicesAsync(
        InMemoryProcessedItemsService processedItems) =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(ServiceBusWithActionsTriggerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateHostBuilder)
            .ConfigureFakeServiceBusMessageActions()
            .ConfigureServices(services => services.AddSingleton<IProcessedItemsService>(processedItems))
            .BuildAndStartAsync(TestCancellation);
}
