using AzureFunctions.TestFramework.Redis;
using Microsoft.Extensions.DependencyInjection;

namespace TestProject;

public class RedisTests : RedisTestsBase
{
    public RedisTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(InMemoryProcessedItemsService processedItems) =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(RedisFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateHostBuilder)
            .WithRedisInput(RedisFunction.InputCommand, RedisInputTestValue)
            .ConfigureServices(services => services.AddSingleton<IProcessedItemsService>(processedItems))
            .BuildAndStartAsync(TestCancellation);
}
