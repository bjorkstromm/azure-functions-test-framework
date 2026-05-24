using Microsoft.Azure.Functions.Worker;

namespace TestProject;

public class WarmupTriggerFunction
{
    [Function("WarmupTrigger")]
    public string Run([WarmupTrigger] object context)
    {
        _ = context;
        return "warmup-complete";
    }
}
