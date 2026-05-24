using AzureFunctions.TestFramework.Warmup;
using Xunit;

namespace TestProject;

/// <summary>Tests for warmup-triggered functions.</summary>
public abstract class WarmupTriggerTestsBase : TestHostTestBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected WarmupTriggerTestsBase(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task InvokeWarmupAsync_WithDefaultContext_Succeeds()
    {
        var result = await TestHost.InvokeWarmupAsync("WarmupTrigger", cancellationToken: TestCancellation);
        Assert.True(result.Success, $"Warmup invocation failed: {result.Error}");
        Assert.Equal("warmup-complete", result.ReadReturnValueAs<string>());
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task InvokeWarmupAsync_WithCustomContext_Succeeds()
    {
        var context = new WarmupContext
        {
            Properties = new Dictionary<string, string>
            {
                ["instance"] = "test-instance"
            }
        };

        var result = await TestHost.InvokeWarmupAsync("WarmupTrigger", context, TestCancellation);
        Assert.True(result.Success, $"Warmup invocation failed: {result.Error}");
        Assert.Equal("warmup-complete", result.ReadReturnValueAs<string>());
    }
}
