using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace AzureFunctions.TestFramework.Core.Grpc;

/// <summary>
/// Tracks the gRPC completion of an HTTP invocation forwarded through ASP.NET Core.
/// Disposing a dispatched capture keeps its registration until the worker response arrives.
/// </summary>
public sealed class InvocationResponseCapture : IDisposable
{
    private readonly TaskCompletionSource<InvocationResponse> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Action? _release;
    private volatile bool _wasDispatched;

    internal InvocationResponseCapture(string? httpOutputBindingName, Action release)
    {
        HttpOutputBindingName = httpOutputBindingName;
        _release = release;
    }

    /// <summary>Gets the HTTP output binding name, whose payload is handled by ASP.NET Core.</summary>
    public string? HttpOutputBindingName { get; }

    /// <summary>Gets whether the HTTP bridge dispatched the invocation to the worker.</summary>
    public bool WasDispatched => _wasDispatched;

    /// <summary>Gets the worker's invocation response, including non-HTTP output bindings.</summary>
    public Task<InvocationResponse> Response => _completion.Task;

    internal void MarkDispatched() => _wasDispatched = true;

    internal void Complete(InvocationResponse response) => _completion.TrySetResult(response);

    /// <summary>Removes the response capture from the host.</summary>
    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
}
