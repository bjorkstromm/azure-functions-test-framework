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
/// </summary>
internal sealed class AspNetCoreForwardingHandler : HttpMessageHandler
{
    private const string InvocationIdHeader = "x-ms-invocation-id";

    private readonly HttpMessageInvoker _inner;
    private readonly GrpcHostService _grpcHostService;
    private readonly string _routePrefix;

    public AspNetCoreForwardingHandler(
        HttpMessageHandler testServerHandler, GrpcHostService grpcHostService, string routePrefix)
    {
        _inner = new HttpMessageInvoker(testServerHandler, disposeHandler: false);
        _grpcHostService = grpcHostService;
        _routePrefix = routePrefix;
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

        var invocationId = string.Join(",", request.Headers.GetValues(InvocationIdHeader));
        using var capture = _grpcHostService.BeginCaptureInvocationResponse(
            invocationId, request.Method.Method, request.RequestUri!.AbsolutePath, _routePrefix);
        if (capture == null)
        {
            return await _inner.SendAsync(request, cancellationToken);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_grpcHostService.InvocationTimeout);
        var response = await _inner.SendAsync(request, timeout.Token);
        try
        {
            // TestServer can return at response start, before the worker sends its outputs.
            // Drain the body so response writing cannot block invocation completion.
            await response.Content.ReadAsByteArrayAsync(timeout.Token);
            if (capture.WasDispatched)
            {
                var invocationResponse = await capture.Response.WaitAsync(timeout.Token);
                FunctionsHttpOutputData.Capture(response, request, invocationResponse, capture.HttpOutputBindingName);
            }
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
