using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Sample.FunctionApp.Worker;

/// <summary>
/// Example timer-triggered function. In tests, invoke via <c>host.InvokeTimerAsync("HeartbeatTimer")</c>.
/// </summary>
public class HeartbeatTimerFunction
{
    private readonly ILogger<HeartbeatTimerFunction> _logger;

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public HeartbeatTimerFunction(ILogger<HeartbeatTimerFunction> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Function("HeartbeatTimer")]
    public void Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer)
    {
        _logger.LogInformation("Heartbeat timer fired at {Time}", DateTime.UtcNow);

        if (myTimer.IsPastDue)
            _logger.LogWarning("Timer is running late (IsPastDue = true)");
    }
}
