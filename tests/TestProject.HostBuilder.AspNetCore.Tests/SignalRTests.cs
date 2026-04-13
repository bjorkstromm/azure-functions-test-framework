using AzureFunctions.TestFramework.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace TestProject;

public class SignalRTests : SignalRTestsBase
{
    public SignalRTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(InMemoryProcessedItemsService processedItems) =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(SignalRTriggerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateHostBuilder)
            .WithSignalRConnectionInfo(TestSignalRUrl, TestAccessToken)
            .ConfigureServices(services => services.AddSingleton<IProcessedItemsService>(processedItems))
            .BuildAndStartAsync(TestCancellation);
}
