using Azure.Messaging.EventHubs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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

    [Function("ProcessEventHubText")]
    public void RunText(
        [EventHubTrigger("test-hub", Connection = "EventHubConnection", IsBatched = false)] string message)
        => _processedItems.Add(message);

    [Function("ProcessEventHubTextBatch")]
    public void RunTextBatch(
        [EventHubTrigger("test-hub", Connection = "EventHubConnection")] string[] messages)
    {
        foreach (var message in messages)
            _processedItems.Add(message);
    }

    [Function("ProcessEventHubJsonBatch")]
    public void RunJsonBatch(
        [EventHubTrigger("test-hub", Connection = "EventHubConnection")] JsonElement[] messages)
    {
        foreach (var message in messages)
            _processedItems.Add(message.GetRawText());
    }

    [Function("ProcessEventHubBytes")]
    public void RunBytes(
        [EventHubTrigger("test-hub", Connection = "EventHubConnection", IsBatched = false)] byte[] message)
        => _processedItems.Add(Convert.ToBase64String(message));

    [Function("ProcessEventHubBytesBatch")]
    public void RunBytesBatch(
        [EventHubTrigger("test-hub", Connection = "EventHubConnection")] byte[][] messages)
    {
        foreach (var message in messages)
            _processedItems.Add(Convert.ToBase64String(message));
    }

    [Function("ProcessEventHubPayload")]
    public void RunPayload(
        [EventHubTrigger("test-hub", Connection = "EventHubConnection", IsBatched = false)] EventHubPayload message)
        => _processedItems.Add(message.DisplayName);

    [Function("ProcessEventHubPayloadBatch")]
    public void RunPayloadBatch(
        [EventHubTrigger("test-hub", Connection = "EventHubConnection")] EventHubPayload[] messages)
    {
        foreach (var message in messages)
            _processedItems.Add(message.DisplayName);
    }

    [Function("ProcessEventHubEnvelope")]
    public void RunEnvelope(
        [EventHubTrigger("test-hub", Connection = "EventHubConnection", IsBatched = false)] EventData message)
        => _processedItems.Add($"{message.MessageId}|{message.CorrelationId}|{message.Properties["custom"]}|{message.EventBody}");

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

    public sealed class EventHubPayload
    {
        public string DisplayName { get; set; } = "";
    }
}
