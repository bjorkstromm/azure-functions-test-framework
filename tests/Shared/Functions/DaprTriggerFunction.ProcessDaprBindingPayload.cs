using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Dapr;
using Microsoft.Extensions.Logging;

namespace TestProject;

public partial class DaprTriggerFunction
{
    /// <summary>
    /// Records the POCO data delivered by a Dapr input binding trigger.
    /// </summary>
    /// <param name="payload">The JSON-deserialized binding event payload.</param>
    [Function("ProcessDaprBindingPayload")]
    public void ProcessDaprBindingPayload(
        [DaprBindingTrigger(BindingName = BindingName)] DaprPayload payload)
    {
        logger.LogInformation("Dapr binding trigger payload: id={Id} message={Message}",
            payload?.Id, payload?.Message);
        processedItems.Add(payload?.Id ?? string.Empty);
    }
}
