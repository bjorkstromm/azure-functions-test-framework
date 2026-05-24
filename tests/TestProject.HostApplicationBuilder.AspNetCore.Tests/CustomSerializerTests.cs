namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class CustomSerializerTests : CustomSerializerTestsBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public CustomSerializerTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected override Task<IFunctionsTestHost> CreateTestHostWithCustomSerializerAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(HttpTriggerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostApplicationBuilderFactory(TestHostFactory.CreateWebApplicationBuilder)
            .ConfigureServices(ConfigureSnakeCaseSerializer)
            .BuildAndStartAsync(TestCancellation);
}
