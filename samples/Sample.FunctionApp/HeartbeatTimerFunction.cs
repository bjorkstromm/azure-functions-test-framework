using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Sample.FunctionApp;

/// <summary>
/// Example timer-triggered function. Fires on a schedule and logs a heartbeat message.
/// In tests, invoke via <c>host.InvokeTimerAsync("HeartbeatTimer")</c>.
/// </summary>
public class HeartbeatTimerFunction
{
    private readonly ILogger<HeartbeatTimerFunction> _logger;

    public HeartbeatTimerFunction(ILogger<HeartbeatTimerFunction> logger)
    {
        _logger = logger;
    }

    [Function("HeartbeatTimer")]
    public void Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer)
    {
        _logger.LogInformation("Heartbeat timer fired at {Time}", DateTime.UtcNow);

        if (myTimer.IsPastDue)
        {
            _logger.LogWarning("Timer is running late (IsPastDue = true)");
        }

        if (myTimer.ScheduleStatus is { } status)
        {
            _logger.LogInformation(
                "Schedule status — Last: {Last}, Next: {Next}",
                status.Last,
                status.Next);
        }
    }
}
