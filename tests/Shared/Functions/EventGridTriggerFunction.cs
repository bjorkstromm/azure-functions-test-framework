using Azure.Messaging.EventGrid;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TestProject;

public class EventGridTriggerFunction
{
    private readonly ILogger<EventGridTriggerFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    public EventGridTriggerFunction(ILogger<EventGridTriggerFunction> logger, IProcessedItemsService processedItems)
    {
        _logger = logger;
        _processedItems = processedItems;
    }

    [Function("ProcessEventGridEvent")]
    public void Run([EventGridTrigger] EventGridEvent eventGridEvent)
    {
        _logger.LogInformation("Processing Event Grid event: {Subject}", eventGridEvent.Subject);
        _processedItems.Add(eventGridEvent.Subject);
    }
}
