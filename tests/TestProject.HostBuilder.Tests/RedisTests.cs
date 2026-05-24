using AzureFunctions.TestFramework.Redis;
using Microsoft.Extensions.DependencyInjection;

namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class RedisTests : RedisTestsBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public RedisTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected override Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(InMemoryProcessedItemsService processedItems) =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(RedisFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateWorkerHostBuilder)
            .WithRedisInput(RedisFunction.InputCommand, RedisInputTestValue)
            .ConfigureServices(services => services.AddSingleton<IProcessedItemsService>(processedItems))
            .BuildAndStartAsync(TestCancellation);
}
