using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Dapr;
using Microsoft.Extensions.Logging;

namespace TestProject;

public partial class DaprTriggerFunction
{
    /// <summary>
    /// Records the body delivered by a Dapr service invocation trigger.
    /// </summary>
    /// <param name="body">The invocation request body.</param>
    [Function("ProcessDaprInvocation")]
    public void ProcessDaprInvocation(
        [DaprServiceInvocationTrigger] string body)
    {
        logger.LogInformation("Dapr service invocation received: {Body}", body);
        processedItems.Add(body);
    }
}
