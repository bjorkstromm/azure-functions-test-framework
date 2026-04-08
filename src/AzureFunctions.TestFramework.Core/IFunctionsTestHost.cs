namespace AzureFunctions.TestFramework.Core;

/// <summary>
/// Main entry point for hosting and testing Azure Functions in-process.
/// Similar to WebApplicationFactory in ASP.NET Core testing.
/// </summary>
public interface IFunctionsTestHost : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Gets the service provider for accessing configured services.
    /// </summary>
    IServiceProvider Services { get; }

    /// <summary>
    /// Gets the function invoker for triggering functions.
    /// </summary>
    IFunctionInvoker Invoker { get; }

    /// <summary>
    /// Starts the test host and initializes the Functions worker.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the test host and cleans up resources.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
