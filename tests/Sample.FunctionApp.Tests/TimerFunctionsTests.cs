using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Timer;
using Microsoft.Azure.Functions.Worker;
using Sample.FunctionApp;
using Xunit;
using Xunit.Abstractions;

namespace Sample.FunctionApp.Tests;

/// <summary>
/// Integration tests for timer-triggered Azure Functions using <see cref="FunctionsTestHost"/>.
/// Demonstrates invoking timer functions via <see cref="FunctionsTestHostTimerExtensions.InvokeTimerAsync"/>.
/// </summary>
public class TimerFunctionsTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IFunctionsTestHost? _testHost;

    public TimerFunctionsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        var builder = new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(HeartbeatTimerFunction).Assembly);

        _testHost = await builder.BuildAndStartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_testHost != null)
        {
            await _testHost.StopAsync();
            _testHost.Dispose();
        }
    }

    [Fact]
    public async Task InvokeTimerAsync_WithDefaultTimerInfo_Succeeds()
    {
        // Act — no TimerInfo provided; framework uses a default (IsPastDue = false)
        var result = await _testHost!.InvokeTimerAsync("HeartbeatTimer");

        _output.WriteLine($"InvocationId: {result.InvocationId}");
        _output.WriteLine($"Success: {result.Success}");
        if (result.Error != null)
        {
            _output.WriteLine($"Error: {result.Error}");
        }

        // Assert
        Assert.True(result.Success, $"Expected invocation to succeed but got error: {result.Error}");
    }

    [Fact]
    public async Task InvokeTimerAsync_WithIsPastDue_Succeeds()
    {
        // Arrange — simulate a timer that is past due
        var timerInfo = new TimerInfo
        {
            IsPastDue = true,
            ScheduleStatus = new ScheduleStatus
            {
                Last = DateTime.UtcNow.AddMinutes(-10),
                Next = DateTime.UtcNow.AddMinutes(5),
                LastUpdated = DateTime.UtcNow.AddMinutes(-10)
            }
        };

        // Act
        var result = await _testHost!.InvokeTimerAsync("HeartbeatTimer", timerInfo);

        _output.WriteLine($"InvocationId: {result.InvocationId}");
        _output.WriteLine($"Success: {result.Success}");

        // Assert
        Assert.True(result.Success, $"Expected invocation to succeed but got error: {result.Error}");
    }

    [Fact]
    public async Task InvokeTimerAsync_UnknownFunction_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _testHost!.InvokeTimerAsync("NonExistentTimer"));
    }
}
