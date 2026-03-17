using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Durable;

/// <summary>
/// Direct durable activity invocation helpers for <see cref="IFunctionsTestHost"/>.
/// </summary>
public static class FunctionsTestHostDurableActivityExtensions
{
    /// <summary>
    /// Executes a fake durable activity directly through the configured durable test services.
    /// </summary>
    /// <typeparam name="TResult">The expected activity result type.</typeparam>
    /// <param name="host">The started functions test host.</param>
    /// <param name="functionName">The durable activity function name.</param>
    /// <param name="input">Optional activity input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The typed activity result.</returns>
    public static async Task<TResult?> InvokeActivityAsync<TResult>(
        this IFunctionsTestHost host,
        string functionName,
        object? input = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);

        var runner = host.Services.GetService(typeof(FakeDurableOrchestrationRunner)) as FakeDurableOrchestrationRunner;
        if (runner is null)
        {
            throw new InvalidOperationException(
                "Fake durable activity invocation requires ConfigureFakeDurableSupport(...) to be registered on the FunctionsTestHostBuilder.");
        }

        var result = await runner.InvokeActivityAsync(functionName, input, cancellationToken).ConfigureAwait(false);
        return (TResult?)FakeDurableOrchestrationRunner.ConvertValue(result, typeof(TResult));
    }
}
