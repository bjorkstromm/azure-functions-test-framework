using Azure.Messaging.EventGrid;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Sample.FunctionApp.Worker;

/// <summary>
/// Example Event Grid–triggered function. In tests, invoke via
/// <c>host.InvokeEventGridAsync("ProcessEventGridEvent", eventGridEvent)</c>.
/// </summary>
public class EventGridFunction
{
    private readonly ILogger<EventGridFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    public EventGridFunction(ILogger<EventGridFunction> logger, IProcessedItemsService processedItems)
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
