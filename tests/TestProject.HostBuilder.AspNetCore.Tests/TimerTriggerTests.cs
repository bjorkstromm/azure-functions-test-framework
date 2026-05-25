
namespace TestProject;

public class TimerTriggerTests : TimerTriggerTestsBase
{
    public TimerTriggerTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TimerTriggerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateWorkerHostBuilder)
            .BuildAndStartAsync(TestCancellation);
}
