using Azure.Messaging.EventHubs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TestProject;

/// <summary>
/// Functions that exercise Event Hubs trigger and output bindings.
/// </summary>
public class EventHubTriggerFunction
{
    private readonly ILogger<EventHubTriggerFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    public EventHubTriggerFunction(ILogger<EventHubTriggerFunction> logger, IProcessedItemsService processedItems)
    {
        _logger = logger;
        _processedItems = processedItems;
    }

    /// <summary>
    /// Event Hubs batch trigger function (default IsBatched = true).
    /// Receives a batch of events and records each event body.
    /// </summary>
    [Function("ProcessEventHubBatch")]
    public void RunBatch(
        [EventHubTrigger("test-hub", Connection = "EventHubConnection")]
        EventData[] events)
    {
        foreach (var eventData in events)
        {
            var body = eventData.EventBody.ToString();
            _logger.LogInformation("Processing Event Hubs batch event: {Body}", body);
            _processedItems.Add(body);
        }
    }

    /// <summary>
    /// Event Hubs single-event trigger function (IsBatched = false).
    /// Receives a single event and records its body.
    /// </summary>
    [Function("ProcessEventHubMessage")]
    public void RunSingle(
        [EventHubTrigger("test-hub", Connection = "EventHubConnection", IsBatched = false)]
        EventData eventData)
    {
        var body = eventData.EventBody.ToString();
        _logger.LogInformation("Processing Event Hubs event: {Body}", body);
        _processedItems.Add(body);
    }

    /// <summary>
    /// Event Hubs single-event trigger function with an Event Hubs output binding.
    /// Returns the event body prefixed with "forwarded:" as the output.
    /// </summary>
    [Function("ForwardEventHubMessage")]
    [EventHubOutput("forwarded-hub", Connection = "EventHubConnection")]
    public string ForwardSingle(
        [EventHubTrigger("test-hub", Connection = "EventHubConnection", IsBatched = false)]
        EventData eventData)
    {
        var body = eventData.EventBody.ToString();
        _logger.LogInformation("Forwarding Event Hubs event: {Body}", body);
        _processedItems.Add(body);
        return $"forwarded:{body}";
    }
}
