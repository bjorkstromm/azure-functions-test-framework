using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace AzureFunctions.TestFramework.Core.Grpc;

/// <summary>
/// Lifecycle states of an <see cref="InvocationResponseCapture"/>.
/// </summary>
public enum InvocationResponseCaptureState
{
    /// <summary>Registered; the invocation has not been dispatched to the worker.</summary>
    Pending,

    /// <summary>The invocation request was written to the worker; awaiting its response.</summary>
    Dispatched,

    /// <summary>The worker's invocation response was received.</summary>
    Completed,

    /// <summary>
    /// Released by its owner before the response arrived. A capture abandoned after dispatch keeps
    /// its invocation ID reserved until the worker responds, so a late response can never complete
    /// a later capture that reuses the same ID.
    /// </summary>
    Abandoned
}

/// <summary>
/// Tracks the gRPC completion of an HTTP invocation forwarded through ASP.NET Core.
/// Transitions: <see cref="InvocationResponseCaptureState.Pending"/> →
/// <see cref="InvocationResponseCaptureState.Dispatched"/> →
/// <see cref="InvocationResponseCaptureState.Completed"/>, or
/// <see cref="InvocationResponseCaptureState.Abandoned"/> when disposed before completion.
/// All transitions are performed by <see cref="GrpcHostService"/> under its lock.
/// </summary>
public sealed class InvocationResponseCapture : IDisposable
{
    private readonly GrpcHostService _owner;
    private readonly TaskCompletionSource<InvocationResponse> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile InvocationResponseCaptureState _state = InvocationResponseCaptureState.Pending;

    internal InvocationResponseCapture(GrpcHostService owner, string invocationId)
    {
        _owner = owner;
        InvocationId = invocationId;
    }

    /// <summary>Gets the invocation ID this capture is registered under.</summary>
    public string InvocationId { get; }

    /// <summary>Gets the current lifecycle state.</summary>
    public InvocationResponseCaptureState State
    {
        get => _state;
        internal set => _state = value;
    }

    /// <summary>
    /// Gets the output metadata of the function the invocation was dispatched to, or
    /// <see langword="null"/> when the invocation was never dispatched.
    /// </summary>
    public HttpFunctionOutputInfo? OutputInfo { get; internal set; }

    /// <summary>Gets the worker's invocation response, including non-HTTP output bindings.</summary>
    public Task<InvocationResponse> Response => _completion.Task;

    internal void Complete(InvocationResponse response) => _completion.TrySetResult(response);

    /// <summary>Releases the capture; see <see cref="InvocationResponseCaptureState.Abandoned"/>.</summary>
    public void Dispose() => _owner.ReleaseInvocationResponseCapture(this);
}

/// <summary>
/// Output binding metadata for an HTTP-triggered function.
/// </summary>
/// <param name="HttpOutputBindingName">
/// The HTTP output binding name (e.g. <c>"$return"</c> or <c>"HttpResponse"</c>), or
/// <see langword="null"/> when the function declares no HTTP output binding.
/// </param>
/// <param name="HasNonHttpOutputs">Whether the function declares any non-HTTP output binding.</param>
/// <param name="HasNonHttpReturnValue">
/// Whether the function's <c>$return</c> binding is a non-HTTP output (e.g. <c>[QueueOutput]</c>
/// on the method). The worker sends that value as the invocation's return value.
/// </param>
public sealed record HttpFunctionOutputInfo(
    string? HttpOutputBindingName, bool HasNonHttpOutputs, bool HasNonHttpReturnValue)
{
    /// <summary>Metadata for a function without output bindings.</summary>
    public static HttpFunctionOutputInfo None { get; } = new(null, false, false);
}
