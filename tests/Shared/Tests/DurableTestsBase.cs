using AzureFunctions.TestFramework.Durable;
using Xunit;

namespace TestProject;

/// <summary>Simple durable tests: DurableTaskClient invocation + entity.</summary>
public abstract class DurableTestsBase : TestHostTestBase
{
    protected DurableTestsBase(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task StartOrchestration_ReturnsExpectedOutput()
    {
        var response = await Client.GetAsync("/api/durable/start/world", TestCancellation);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(TestCancellation);
        Assert.Contains("Hello", content);
        Assert.Contains("world", content);
    }
}
