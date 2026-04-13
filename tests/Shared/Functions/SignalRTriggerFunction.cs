using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace TestProject;

/// <summary>
/// Functions that exercise SignalR trigger, input, and output bindings.
/// </summary>
public class SignalRTriggerFunction
{
    private readonly ILogger<SignalRTriggerFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    public SignalRTriggerFunction(
        ILogger<SignalRTriggerFunction> logger,
        IProcessedItemsService processedItems)
    {
        _logger = logger;
        _processedItems = processedItems;
    }

    /// <summary>
    /// SignalR message trigger function.
    /// Records the connection ID and event name so tests can verify trigger invocation.
    /// </summary>
    [Function("ProcessSignalRMessage")]
    public void Run(
        [SignalRTrigger("chat", "messages", "sendMessage")] SignalRInvocationContext invocationContext)
    {
        _logger.LogInformation(
            "SignalR message received: hub={Hub}, event={Event}, connectionId={ConnectionId}",
            invocationContext.Hub, invocationContext.Event, invocationContext.ConnectionId);

        _processedItems.Add($"{invocationContext.ConnectionId}:{invocationContext.Event}");
    }

    /// <summary>
    /// SignalR connection trigger function.
    /// Records the connection ID so tests can verify connection events.
    /// </summary>
    [Function("ProcessSignalRConnection")]
    public void OnConnected(
        [SignalRTrigger("chat", "connections", "connected")] SignalRInvocationContext invocationContext)
    {
        _logger.LogInformation(
            "SignalR client connected: hub={Hub}, connectionId={ConnectionId}",
            invocationContext.Hub, invocationContext.ConnectionId);

        _processedItems.Add($"connected:{invocationContext.ConnectionId}");
    }

    /// <summary>
    /// SignalR trigger function with a SignalR output binding.
    /// Broadcasts the received message back to all clients in a group
    /// and returns the message action as the captured return value.
    /// </summary>
    [Function("BroadcastSignalRMessage")]
    [SignalROutput(HubName = "chat")]
    public SignalRMessageAction? Broadcast(
        [SignalRTrigger("chat", "messages", "broadcast")] SignalRInvocationContext invocationContext)
    {
        var text = invocationContext.Arguments?.FirstOrDefault()?.ToString() ?? string.Empty;
        _logger.LogInformation("Broadcasting SignalR message: {Text}", text);
        _processedItems.Add(invocationContext.ConnectionId);
        return new SignalRMessageAction("broadcast", [$"echo:{text}"]);
    }

    /// <summary>
    /// Queue-triggered function that reads a <c>[SignalRConnectionInfoInput]</c> binding
    /// and records the connection URL so tests can verify synthetic binding injection.
    /// </summary>
    [Function("ReadSignalRConnectionInfo")]
    public void ReadConnectionInfo(
        [QueueTrigger("signalr-queue")] string unused,
        [SignalRConnectionInfoInput(HubName = "chat")] SignalRConnectionInfo? connInfo)
    {
        if (connInfo is not null)
        {
            _logger.LogInformation("SignalR connection info: url={Url}", connInfo.Url);
            _processedItems.Add(connInfo.Url ?? "null-url");
        }
        else
        {
            _processedItems.Add("null-conninfo");
        }
    }
}
