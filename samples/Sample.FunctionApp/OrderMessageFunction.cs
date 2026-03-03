using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Sample.FunctionApp;

/// <summary>
/// Example Service Bus–triggered function. Processes messages from a queue.
/// In tests, invoke via <c>host.InvokeServiceBusAsync("ProcessOrderMessage", message)</c>.
/// </summary>
public class OrderMessageFunction
{
    private readonly ILogger<OrderMessageFunction> _logger;

    public OrderMessageFunction(ILogger<OrderMessageFunction> logger)
    {
        _logger = logger;
    }

    [Function("ProcessOrderMessage")]
    public void Run(
        [ServiceBusTrigger("orders-queue", Connection = "ServiceBusConnection")] string messageBody)
    {
        _logger.LogInformation("Processing order message: {Body}", messageBody);
    }
}
