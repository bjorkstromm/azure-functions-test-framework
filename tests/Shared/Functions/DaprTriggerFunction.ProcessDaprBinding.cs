using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Dapr;
using Microsoft.Extensions.Logging;

namespace TestProject;

public partial class DaprTriggerFunction
{
    /// <summary>
    /// Records the string data delivered by a Dapr input binding trigger.
    /// </summary>
    /// <param name="data">The binding event data.</param>
    [Function("ProcessDaprBinding")]
    public void ProcessDaprBinding(
        [DaprBindingTrigger(BindingName = BindingName)] string data)
    {
        logger.LogInformation("Dapr binding trigger received: {Data}", data);
        processedItems.Add(data);
    }
}
