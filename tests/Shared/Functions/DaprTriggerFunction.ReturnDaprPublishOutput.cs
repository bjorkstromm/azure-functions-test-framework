using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Dapr;

namespace TestProject;

public partial class DaprTriggerFunction
{
    /// <summary>
    /// Returns a POCO whose property carries the <c>DaprPublishOutput</c> binding payload.
    /// </summary>
    /// <param name="data">The inbound Dapr binding trigger data.</param>
    /// <returns>The outbound binding result.</returns>
    [Function("ReturnDaprPublishOutput")]
    public DaprPublishOutputResult ReturnDaprPublishOutput(
        [DaprBindingTrigger(BindingName = BindingName)] string data)
    {
        return new DaprPublishOutputResult { OutboundMessage = $"dapr-out:{data}" };
    }
}
