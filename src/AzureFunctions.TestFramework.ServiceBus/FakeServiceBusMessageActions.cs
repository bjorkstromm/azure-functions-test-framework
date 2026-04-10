using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;

namespace AzureFunctions.TestFramework.ServiceBus;

/// <summary>
/// Recorded call to a <see cref="ServiceBusMessageActions"/> settlement method.
/// </summary>
/// <param name="Action">The action name (Complete, Abandon, DeadLetter, Defer, RenewLock).</param>
/// <param name="Message">The <see cref="ServiceBusReceivedMessage"/> the action was called on.</param>
/// <param name="Properties">Optional properties passed with the action (e.g. <c>propertiesToModify</c>).</param>
/// <param name="DeadLetterReason">Reason supplied when dead-lettering, if applicable.</param>
/// <param name="DeadLetterErrorDescription">Error description supplied when dead-lettering, if applicable.</param>
public sealed record ServiceBusMessageActionRecord(
    string Action,
    ServiceBusReceivedMessage Message,
    IDictionary<string, object>? Properties = null,
    string? DeadLetterReason = null,
    string? DeadLetterErrorDescription = null);

/// <summary>
/// A testable fake implementation of <see cref="ServiceBusMessageActions"/> that records all
/// settlement calls instead of communicating with the real Azure Service Bus settlement gRPC endpoint.
/// </summary>
/// <remarks>
/// Register this via
/// <see cref="FunctionsTestHostBuilderServiceBusExtensions.ConfigureFakeServiceBusMessageActions"/>.
/// Resolve it from the worker's <see cref="IServiceProvider"/> (via <c>host.Services</c>) to assert
/// which actions were called during a test invocation.
/// </remarks>
public class FakeServiceBusMessageActions : ServiceBusMessageActions
{
    private readonly List<ServiceBusMessageActionRecord> _recorded = [];

    /// <summary>
    /// Gets a snapshot of all settlement actions recorded so far, in the order they were called.
    /// </summary>
    public IReadOnlyList<ServiceBusMessageActionRecord> RecordedActions => _recorded;

    /// <summary>
    /// Clears all previously recorded actions.
    /// </summary>
    public void Reset() => _recorded.Clear();

    /// <inheritdoc />
    public override Task CompleteMessageAsync(
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        _recorded.Add(new ServiceBusMessageActionRecord("Complete", message));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task AbandonMessageAsync(
        ServiceBusReceivedMessage message,
        IDictionary<string, object>? propertiesToModify = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        _recorded.Add(new ServiceBusMessageActionRecord("Abandon", message, propertiesToModify));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeadLetterMessageAsync(
        ServiceBusReceivedMessage message,
        Dictionary<string, object>? propertiesToModify = null,
        string? deadLetterReason = null,
        string? deadLetterErrorDescription = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        _recorded.Add(new ServiceBusMessageActionRecord(
            "DeadLetter", message, propertiesToModify, deadLetterReason, deadLetterErrorDescription));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeferMessageAsync(
        ServiceBusReceivedMessage message,
        IDictionary<string, object>? propertiesToModify = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        _recorded.Add(new ServiceBusMessageActionRecord("Defer", message, propertiesToModify));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task RenewMessageLockAsync(
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        _recorded.Add(new ServiceBusMessageActionRecord("RenewLock", message));
        return Task.CompletedTask;
    }
}
