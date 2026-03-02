namespace AzureFunctions.TestFramework.Core;

/// <summary>
/// Manages the Azure Functions worker lifecycle and communication.
/// </summary>
public interface IWorkerHost : IAsyncDisposable
{
    /// <summary>
    /// Gets a value indicating whether the worker is initialized and ready.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Initializes and starts the Functions worker.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the Functions worker.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message to the worker and waits for a response.
    /// </summary>
    Task<WorkerMessage> SendMessageAsync(
        WorkerMessage message,
        CancellationToken cancellationToken = default);
}
