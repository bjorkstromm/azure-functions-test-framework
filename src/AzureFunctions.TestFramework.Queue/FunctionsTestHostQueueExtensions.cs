using Azure.Storage.Queues.Models;
using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Queue;

/// <summary>
/// Extension methods for invoking queue-triggered Azure Functions via <see cref="IFunctionsTestHost"/>.
/// </summary>
public static class FunctionsTestHostQueueExtensions
{
    /// <summary>
    /// Invokes a queue-triggered function by name with the specified <see cref="QueueMessage"/>.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the queue function (case-insensitive).</param>
    /// <param name="message">The queue message to pass to the function.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeQueueAsync(
        this IFunctionsTestHost host,
        string functionName,
        QueueMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var body = message.Body?.ToArray() ?? [];

        var context = new FunctionInvocationContext
        {
            TriggerType = "queueTrigger",
            InputData = { ["$queueMessageBytes"] = body }
        };

        return host.Invoker.InvokeAsync(functionName, context, cancellationToken);
    }
}
