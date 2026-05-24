using Azure.Messaging.EventGrid;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TestProject;

/// <summary>
/// Functions that exercise output bindings for ServiceBus and EventGrid.
/// Output binding values are captured generically via <c>FunctionInvocationResult.OutputData</c>.
/// </summary>
public class OutputBindingFunction
{
    private readonly ILogger<OutputBindingFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    public OutputBindingFunction(ILogger<OutputBindingFunction> logger, IProcessedItemsService processedItems)
    {
        _logger = logger;
        _processedItems = processedItems;
    }

    /// <summary>
    /// Queue-triggered function that writes to a ServiceBus output binding.
    /// </summary>
    [Function("CreateServiceBusOutputMessage")]
    public ServiceBusOutputBindingResult CreateServiceBusOutputMessage(
        [QueueTrigger("servicebus-output-queue")] string message)
    {
        _logger.LogInformation("Creating ServiceBus output for {Message}", message);
        _processedItems.Add(message);
        return new ServiceBusOutputBindingResult { OutputMessage = $"sb:{message}" };
    }

    /// <summary>
    /// Queue-triggered function that writes to an EventGrid output binding.
    /// </summary>
    [Function("CreateEventGridOutputEvent")]
    public EventGridOutputBindingResult CreateEventGridOutputEvent(
        [QueueTrigger("eventgrid-output-queue")] string message)
    {
        _logger.LogInformation("Creating EventGrid output for {Message}", message);
        _processedItems.Add(message);
        return new EventGridOutputBindingResult { OutputEvent = $"eg:{message}" };
    }
}

public sealed class ServiceBusOutputBindingResult
{
    [ServiceBusOutput("captured-output-topic")]
    public string OutputMessage { get; set; } = string.Empty;
}

public sealed class EventGridOutputBindingResult
{
    [EventGridOutput(TopicEndpointUri = "https://fake.eventgrid.topic.endpoint", TopicKeySetting = "TopicKey")]
    public string OutputEvent { get; set; } = string.Empty;
}
