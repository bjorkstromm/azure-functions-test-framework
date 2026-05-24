
namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class TimerTriggerTests : TimerTriggerTestsBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public TimerTriggerTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TimerTriggerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostApplicationBuilderFactory(TestHostFactory.CreateApplicationBuilder)
            .BuildAndStartAsync(TestCancellation);
}
