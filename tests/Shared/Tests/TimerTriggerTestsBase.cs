using AzureFunctions.TestFramework.Timer;
using Xunit;

namespace TestProject;

/// <summary>Tests for timer-triggered functions.</summary>
public abstract class TimerTriggerTestsBase : TestHostTestBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected TimerTriggerTestsBase(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task InvokeTimerAsync_WithDefaultTimerInfo_Succeeds()
    {
        var result = await TestHost.InvokeTimerAsync("TimerTrigger", cancellationToken: TestCancellation);
        Assert.True(result.Success, $"Timer invocation failed: {result.Error}");
    }
}
