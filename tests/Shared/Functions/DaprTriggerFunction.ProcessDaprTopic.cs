using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Dapr;
using Microsoft.Extensions.Logging;

namespace TestProject;

public partial class DaprTriggerFunction
{
    /// <summary>
    /// Records the message delivered by a Dapr pub/sub topic trigger.
    /// </summary>
    /// <param name="message">The topic message data.</param>
    [Function("ProcessDaprTopic")]
    public void ProcessDaprTopic(
        [DaprTopicTrigger(PubSubName, Topic = TopicName)] string message)
    {
        logger.LogInformation("Dapr topic trigger received on '{PubSub}/{Topic}': {Message}",
            PubSubName, TopicName, message);
        processedItems.Add(message);
    }
}
