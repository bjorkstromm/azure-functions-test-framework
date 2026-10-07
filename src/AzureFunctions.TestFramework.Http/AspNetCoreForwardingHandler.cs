using AzureFunctions.TestFramework.Core.Grpc;

namespace AzureFunctions.TestFramework.Http;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that forwards requests to the worker's internal
/// ASP.NET Core HTTP server (used when the worker is started with
/// <c>ConfigureFunctionsWebApplication()</c>).
/// <para>
/// The in-memory TestServer handler is used; the URI is passed through unchanged
/// (TestServer routes by path). A synthetic <c>x-ms-invocation-id</c> header is
/// injected when absent.
/// </para>
/// <para>
/// Route-to-function resolution happens exactly once, inside the gRPC bridge
/// (<see cref="GrpcHostService.SendInvocationRequestAsync"/>) that dispatches the request to the
/// worker. This handler never matches routes itself — it only inspects the capture's
/// <see cref="InvocationResponseCapture.State"/> and <see cref="InvocationResponseCapture.OutputInfo"/>
/// after dispatch, so the two can never disagree about which function a request belongs to.
/// </para>
/// </summary>
internal sealed class AspNetCoreForwardingHandler : HttpMessageHandler
{
    private const string InvocationIdHeader = "x-ms-invocation-id";

    private readonly HttpMessageInvoker _inner;
    private readonly GrpcHostService _grpcHostService;

    public AspNetCoreForwardingHandler(HttpMessageHandler testServerHandler, GrpcHostService grpcHostService)
    {
        _inner = new HttpMessageInvoker(testServerHandler, disposeHandler: false);
        _grpcHostService = grpcHostService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Inject a synthetic invocation ID if the caller didn't provide one.
        if (!request.Headers.Contains(InvocationIdHeader))
        {
            request.Headers.TryAddWithoutValidation(InvocationIdHeader, Guid.NewGuid().ToString());
        }

        // A single caller-provided value is required: silently joining multiple values would
        // produce a garbage composite capture key instead of failing fast on a violated invariant.
        var invocationId = request.Headers.GetValues(InvocationIdHeader).Single();
        using var capture = _grpcHostService.BeginCaptureInvocationResponse(invocationId);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_grpcHostService.InvocationTimeout);
        var response = await _inner.SendAsync(request, timeout.Token);
        try
        {
            // Dispatch (if any) has already happened by now: the gRPC bridge middleware runs
            // before the rest of the ASP.NET Core pipeline that produces this response. OutputInfo
            // is set once, at dispatch time, and never cleared — so it (not State, which may have
            // already raced ahead to Completed for a fast invocation) is what tells us whether this
            // request was ever dispatched.
            if (capture.OutputInfo?.HasNonHttpOutputs != true)
            {
                return response;
            }

            // TestServer can return at response start, before the worker sends its outputs.
            // Drain the body so response writing cannot block invocation completion.
            await response.Content.ReadAsByteArrayAsync(timeout.Token);
            var invocationResponse = await capture.Response.WaitAsync(timeout.Token);
            FunctionsHttpOutputData.Capture(response, request, invocationResponse, capture.OutputInfo);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }
        base.Dispose(disposing);
    }
}
