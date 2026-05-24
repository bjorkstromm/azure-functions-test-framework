using Microsoft.Azure.Functions.Worker;

namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class WarmupTriggerFunction
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Function("WarmupTrigger")]
    public string Run([WarmupTrigger] object context)
    {
        _ = context;
        return "warmup-complete";
    }
}
