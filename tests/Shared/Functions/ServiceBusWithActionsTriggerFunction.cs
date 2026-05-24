using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class ServiceBusWithActionsTriggerFunction
{
    private readonly ILogger<ServiceBusWithActionsTriggerFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public ServiceBusWithActionsTriggerFunction(ILogger<ServiceBusWithActionsTriggerFunction> logger, IProcessedItemsService processedItems)
    {
        _logger = logger;
        _processedItems = processedItems;
    }

    /// <summary>
    /// Represents this member.
    /// </summary>
    [Function("ProcessServiceBusMessageWithActions")]
    public async Task Run(
        [ServiceBusTrigger("test-actions-topic", "test-actions-subscription")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions)
    {
        var body = message.Body.ToString();
        _logger.LogInformation("Processing Service Bus message with actions: {Body}", body);
        _processedItems.Add(body);

        // Complete the message using the injected actions
        await actions.CompleteMessageAsync(message);
    }
}
