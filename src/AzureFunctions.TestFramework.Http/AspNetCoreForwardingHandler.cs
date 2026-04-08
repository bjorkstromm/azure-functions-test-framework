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

    public AspNetCoreForwardingHandler(HttpMessageHandler testServerHandler)
    {
        _inner = new HttpMessageInvoker(testServerHandler, disposeHandler: false);
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

        return await _inner.SendAsync(request, cancellationToken);
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
