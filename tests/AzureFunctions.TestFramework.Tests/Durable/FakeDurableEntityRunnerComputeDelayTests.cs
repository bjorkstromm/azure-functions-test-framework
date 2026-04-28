using AzureFunctions.TestFramework.Durable;
using Microsoft.DurableTask.Entities;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Durable;

/// <summary>
/// Unit tests for <see cref="FakeDurableEntityRunner.ComputeDelay"/>.
/// </summary>
public class FakeDurableEntityRunnerComputeDelayTests
{
    [Fact]
    public void ComputeDelay_NullOptions_ReturnsZero()
    {
        var delay = FakeDurableEntityRunner.ComputeDelay(null);
        Assert.Equal(TimeSpan.Zero, delay);
    }

    [Fact]
    public void ComputeDelay_OptionsWithNoSignalTime_ReturnsZero()
    {
        var options = new SignalEntityOptions();
        var delay = FakeDurableEntityRunner.ComputeDelay(options);
        Assert.Equal(TimeSpan.Zero, delay);
    }

    [Fact]
    public void ComputeDelay_SignalTimeInPast_ReturnsZero()
    {
        var options = new SignalEntityOptions
        {
            SignalTime = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        var delay = FakeDurableEntityRunner.ComputeDelay(options);
        Assert.Equal(TimeSpan.Zero, delay);
    }

    [Fact]
    public void ComputeDelay_SignalTimeAtExactlyNow_ReturnsZero()
    {
        var options = new SignalEntityOptions
        {
            SignalTime = DateTimeOffset.UtcNow
        };
        var delay = FakeDurableEntityRunner.ComputeDelay(options);
        Assert.Equal(TimeSpan.Zero, delay);
    }

    [Fact]
    public void ComputeDelay_SignalTimeInFuture_ReturnsPositiveDelay()
    {
        var future = DateTimeOffset.UtcNow.AddSeconds(10);
        var options = new SignalEntityOptions { SignalTime = future };

        var delay = FakeDurableEntityRunner.ComputeDelay(options);

        Assert.True(delay > TimeSpan.Zero);
        Assert.True(delay <= TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void ComputeDelay_SignalTimeFarFuture_ReturnsClamped()
    {
        // Far-future value that exceeds int.MaxValue milliseconds.
        var options = new SignalEntityOptions
        {
            SignalTime = DateTimeOffset.UtcNow.AddDays(30)
        };

        var delay = FakeDurableEntityRunner.ComputeDelay(options);
        var maxDelay = TimeSpan.FromMilliseconds(int.MaxValue);

        Assert.True(delay > TimeSpan.Zero);
        Assert.True(delay <= maxDelay, $"Delay {delay} should be clamped to {maxDelay}");
    }
}
