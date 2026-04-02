using AzureFunctions.TestFramework.Core;
using Microsoft.DurableTask.Client.Entities;
using Microsoft.DurableTask.Entities;

namespace AzureFunctions.TestFramework.Durable;

/// <summary>
/// Direct durable entity dispatch helpers for <see cref="IFunctionsTestHost"/>.
/// </summary>
public static class FunctionsTestHostDurableEntityExtensions
{
    /// <summary>
    /// Signals a durable entity (fire-and-forget) and waits for the operation to complete
    /// in-process before returning.
    /// </summary>
    /// <param name="host">The started functions test host.</param>
    /// <param name="entityId">The entity instance identifier.</param>
    /// <param name="operationName">The operation name to invoke on the entity.</param>
    /// <param name="input">Optional operation input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task SignalEntityAsync(
        this IFunctionsTestHost host,
        EntityInstanceId entityId,
        string operationName,
        object? input = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        return GetRequiredEntityRunner(host).SignalEntityAsync(entityId, operationName, input, cancellationToken);
    }

    /// <summary>
    /// Calls a durable entity and returns its typed result.
    /// </summary>
    /// <typeparam name="TResult">The expected result type.</typeparam>
    /// <param name="host">The started functions test host.</param>
    /// <param name="entityId">The entity instance identifier.</param>
    /// <param name="operationName">The operation name to invoke on the entity.</param>
    /// <param name="input">Optional operation input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The typed operation result.</returns>
    public static async Task<TResult?> CallEntityAsync<TResult>(
        this IFunctionsTestHost host,
        EntityInstanceId entityId,
        string operationName,
        object? input = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        var result = await GetRequiredEntityRunner(host)
            .CallEntityAsync(entityId, operationName, input, cancellationToken)
            .ConfigureAwait(false);
        return (TResult?)FakeDurableOrchestrationRunner.ConvertValue(result, typeof(TResult));
    }

    /// <summary>
    /// Returns the current state of a durable entity. If the entity has not been initialized,
    /// an <see cref="EntityMetadata{TState}"/> with the default state value is returned.
    /// </summary>
    /// <typeparam name="TState">The entity state type.</typeparam>
    /// <param name="host">The started functions test host.</param>
    /// <param name="entityId">The entity instance identifier.</param>
    /// <returns>
    /// An <see cref="EntityMetadata{TState}"/> snapshot. If the entity has not been initialized,
    /// the <see cref="EntityMetadata{TState}.State"/> will be the default value for <typeparamref name="TState"/>.
    /// </returns>
    public static EntityMetadata<TState> GetEntity<TState>(
        this IFunctionsTestHost host,
        EntityInstanceId entityId)
    {
        ArgumentNullException.ThrowIfNull(host);

        return GetRequiredEntityRunner(host).GetEntity<TState>(entityId);
    }

    private static FakeDurableEntityRunner GetRequiredEntityRunner(IFunctionsTestHost host)
    {
        var runner = host.Services.GetService(typeof(FakeDurableEntityRunner)) as FakeDurableEntityRunner;
        if (runner is null)
        {
            throw new InvalidOperationException(
                "Fake durable entity dispatch requires ConfigureFakeDurableSupport(...) to be registered on the FunctionsTestHostBuilder.");
        }

        return runner;
    }
}
