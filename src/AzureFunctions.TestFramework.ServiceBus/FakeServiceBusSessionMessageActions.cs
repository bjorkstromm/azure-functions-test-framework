using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;

namespace AzureFunctions.TestFramework.ServiceBus;

/// <summary>
/// Recorded call to a <see cref="ServiceBusSessionMessageActions"/> method.
/// </summary>
/// <param name="Action">The action name (GetSessionState, SetSessionState, ReleaseSession, RenewSessionLock).</param>
/// <param name="SessionState">The session state passed to <c>SetSessionStateAsync</c>, if applicable.</param>
public sealed record ServiceBusSessionMessageActionRecord(
    string Action,
    BinaryData? SessionState = null);

/// <summary>
/// A testable fake implementation of <see cref="ServiceBusSessionMessageActions"/> that records all
/// calls instead of communicating with the real Azure Service Bus settlement gRPC endpoint.
/// </summary>
/// <remarks>
/// Register this via
/// <see cref="FunctionsTestHostBuilderServiceBusExtensions.ConfigureFakeServiceBusMessageActions"/>.
/// Resolve it from the worker's <see cref="IServiceProvider"/> (via <c>host.Services</c>) to assert
/// which actions were called during a test invocation.
/// </remarks>
public class FakeServiceBusSessionMessageActions : ServiceBusSessionMessageActions
{
    private readonly List<ServiceBusSessionMessageActionRecord> _recorded = [];

    /// <summary>Gets or sets the value returned by <see cref="GetSessionStateAsync"/>.</summary>
    public BinaryData? SessionState { get; set; }

    /// <inheritdoc />
    public override DateTimeOffset SessionLockedUntil { get; protected set; } = DateTimeOffset.UtcNow.AddMinutes(5);

    /// <summary>
    /// Gets a snapshot of all session action calls recorded so far, in the order they were called.
    /// </summary>
    public IReadOnlyList<ServiceBusSessionMessageActionRecord> RecordedActions => _recorded;

    /// <summary>
    /// Clears all previously recorded actions.
    /// </summary>
    public void Reset() => _recorded.Clear();

    /// <inheritdoc />
    public override Task<BinaryData> GetSessionStateAsync(CancellationToken cancellationToken = default)
    {
        _recorded.Add(new ServiceBusSessionMessageActionRecord("GetSessionState"));
        return Task.FromResult(SessionState ?? BinaryData.Empty);
    }

    /// <inheritdoc />
    public override Task SetSessionStateAsync(BinaryData sessionState, CancellationToken cancellationToken = default)
    {
        SessionState = sessionState;
        _recorded.Add(new ServiceBusSessionMessageActionRecord("SetSessionState", sessionState));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task ReleaseSession(CancellationToken cancellationToken = default)
    {
        _recorded.Add(new ServiceBusSessionMessageActionRecord("ReleaseSession"));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task RenewSessionLockAsync(CancellationToken cancellationToken = default)
    {
        _recorded.Add(new ServiceBusSessionMessageActionRecord("RenewSessionLock"));
        return Task.CompletedTask;
    }
}
