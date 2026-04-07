using AzureFunctions.TestFramework.Timer;
using Xunit;

namespace TestProject;

/// <summary>Tests for timer-triggered functions.</summary>
public abstract class TimerTriggerTestsBase : TestHostTestBase
{
    protected TimerTriggerTestsBase(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task InvokeTimerAsync_WithDefaultTimerInfo_Succeeds()
    {
        var result = await TestHost.InvokeTimerAsync("TimerTrigger", cancellationToken: TestCancellation);
        Assert.True(result.Success, $"Timer invocation failed: {result.Error}");
    }
}
