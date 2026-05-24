using AzureFunctions.TestFramework.ServiceBus;
using Microsoft.Extensions.DependencyInjection;

namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class ServiceBusActionsTests : ServiceBusActionsTestsBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public ServiceBusActionsTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    protected override Task<IFunctionsTestHost> CreateTestHostWithServicesAsync(
        InMemoryProcessedItemsService processedItems) =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(ServiceBusWithActionsTriggerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostApplicationBuilderFactory(TestHostFactory.CreateWebApplicationBuilder)
            .ConfigureFakeServiceBusMessageActions()
            .ConfigureServices(services => services.AddSingleton<IProcessedItemsService>(processedItems))
            .BuildAndStartAsync(TestCancellation);
}
