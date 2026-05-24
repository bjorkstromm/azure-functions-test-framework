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

    /// <summary>
    /// Executes this operation.
    /// </summary>
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

    /// <summary>
    /// Queue-triggered function that writes to a SendGrid output binding.
    /// The SendGrid output is captured generically via <c>FunctionInvocationResult.OutputData</c> —
    /// no dedicated framework package is needed.
    /// </summary>
    [Function("CreateSendGridOutputEmail")]
    public SendGridOutputBindingResult CreateSendGridOutputEmail(
        [QueueTrigger("sendgrid-output-queue")] string message)
    {
        _logger.LogInformation("Creating SendGrid output for {Message}", message);
        _processedItems.Add(message);
        return new SendGridOutputBindingResult { OutputEmail = $"sg:{message}" };
    }
}

/// <summary>
/// Represents this type.
/// </summary>
public sealed class ServiceBusOutputBindingResult
{
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    [ServiceBusOutput("captured-output-topic")]
    public string OutputMessage { get; set; } = string.Empty;
}

/// <summary>
/// Represents this type.
/// </summary>
public sealed class EventGridOutputBindingResult
{
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    [EventGridOutput(TopicEndpointUri = "https://fake.eventgrid.topic.endpoint", TopicKeySetting = "TopicKey")]
    public string OutputEvent { get; set; } = string.Empty;
}

/// <summary>
/// Represents this type.
/// </summary>
public sealed class SendGridOutputBindingResult
{
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    [SendGridOutput(ApiKey = "FakeSendGridApiKey")]
    public string OutputEmail { get; set; } = string.Empty;
}
