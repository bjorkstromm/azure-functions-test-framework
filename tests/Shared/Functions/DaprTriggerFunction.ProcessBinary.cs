using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Dapr;

namespace TestProject;

public partial class DaprTriggerFunction
{
    /// <summary>Records the exact bytes received from a Dapr binding.</summary>
    [Function("ProcessDaprBindingBinary")]
    public void ProcessDaprBindingBinary(
        [DaprBindingTrigger(BindingName = BindingName)] byte[] data)
        => processedItems.Add(Convert.ToBase64String(data));

    /// <summary>Records the exact bytes received from a Dapr service invocation.</summary>
    [Function("ProcessDaprInvocationBinary")]
    public void ProcessDaprInvocationBinary(
        [DaprServiceInvocationTrigger] byte[] body)
        => processedItems.Add(Convert.ToBase64String(body));

    /// <summary>Records the exact bytes received from a Dapr topic.</summary>
    [Function("ProcessDaprTopicBinary")]
    public void ProcessDaprTopicBinary(
        [DaprTopicTrigger(PubSubName, Topic = TopicName)] byte[] message)
        => processedItems.Add(Convert.ToBase64String(message));
}
