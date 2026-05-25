using AzureFunctions.TestFramework.Sql;
using Microsoft.Extensions.DependencyInjection;

namespace TestProject;

public class SqlTests : SqlTestsBase
{
    public SqlTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(InMemoryProcessedItemsService processedItems) =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(SqlFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateWorkerHostBuilder)
            .WithSqlInputRows(
                SqlFunction.InputCommandText,
                new SqlProduct { Id = 99, Name = SqlInputTestName })
            .ConfigureServices(services => services.AddSingleton<IProcessedItemsService>(processedItems))
            .BuildAndStartAsync(TestCancellation);
}
