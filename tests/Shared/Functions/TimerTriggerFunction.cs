using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TestProject;

public class TimerTriggerFunction
{
    private readonly ILogger<TimerTriggerFunction> _logger;

    public TimerTriggerFunction(ILogger<TimerTriggerFunction> logger) => _logger = logger;

    [Function("TimerTrigger")]
    public void Run([TimerTrigger("0 */5 * * * *")] TimerInfo timerInfo)
    {
        _logger.LogInformation("Timer fired at {Time}", DateTime.UtcNow);
        if (timerInfo.IsPastDue)
            _logger.LogWarning("Timer is running late");
    }
}
