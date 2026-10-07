using AzureFunctions.TestFramework.Http;
using AzureFunctions.TestFramework.Tests.Core;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using System.Net;
using System.Text;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Http;

public class AspNetCoreForwardingHandlerTests
{
    [Fact]
    public async Task SendAsync_DrainsBodyThenCapturesOutputsAndExcludesClearedHttpBinding()
    {
        var service = InvocationResponseCaptureTests.CreateService();
        var content = new CallbackContent(async () =>
        {
            await service.HandleInvocationResponse(new StreamingMessage
            {
                InvocationResponse = InvocationResponseCaptureTests.SuccessResponse("id")
            });
        });
        using var inner = new CallbackHandler(async (request, _) =>
        {
            Assert.Equal("id", Assert.Single(request.Headers.GetValues("x-ms-invocation-id")));
            await service.SendInvocationRequestAsync("id", "POST", "/v1/items", "v1");
            return new HttpResponseMessage(HttpStatusCode.Created) { Content = content };
        });
        using var handler = new AspNetCoreForwardingHandler(inner, service);
        using var client = new HttpClient(handler);
        using var request = CreateRequest("/v1/items");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
        Assert.True(content.Serialized);
        Assert.Same(request, response.RequestMessage);
        Assert.Equal("queued", Assert.Single(response.GetOutputData()).Value);
        Assert.Equal("body", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        AssertCaptureReleased(service, "id");
    }

    [Fact]
    public async Task SendAsync_NoExtraOutputsDoesNotBufferBody()
    {
        var service = InvocationResponseCaptureTests.CreateService(extraOutputs: false);
        var content = new CallbackContent(() => Task.CompletedTask);
        using var inner = new CallbackHandler(async (_, _) =>
        {
            await service.SendInvocationRequestAsync("id", "POST", "/api/items");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        using var handler = new AspNetCoreForwardingHandler(inner, service);
        using var client = new HttpClient(handler);
        using var request = CreateRequest();
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
        Assert.False(content.Serialized);
        Assert.Empty(response.GetOutputData());
    }

    [Fact]
    public async Task SendAsync_ShortCircuitedPipelineDoesNotWaitForInvocation()
    {
        var service = InvocationResponseCaptureTests.CreateService();
        using var inner = new CallbackHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        using var handler = new AspNetCoreForwardingHandler(inner, service);
        using var client = new HttpClient(handler);
        using var request = CreateRequest();
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(response.GetOutputData());
        AssertCaptureReleased(service, "id");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendAsync_TimeoutOrCancellationReleasesCaptureAndDisposesResponse(bool cancel)
    {
        var service = InvocationResponseCaptureTests.CreateService();
        service.InvocationTimeout = cancel ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(50);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var content = new CallbackContent(() =>
        {
            if (cancel)
            {
                cancellation.Cancel();
            }
            return Task.CompletedTask;
        });
        using var inner = new CallbackHandler(async (_, _) =>
        {
            await service.SendInvocationRequestAsync("id", "POST", "/api/items");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        using var handler = new AspNetCoreForwardingHandler(inner, service);
        using var client = new HttpClient(handler);
        using var request = CreateRequest();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation.Token));
        Assert.True(content.Disposed);
        Assert.Throws<InvalidOperationException>(() => service.BeginCaptureInvocationResponse("id"));
        await service.HandleInvocationResponse(new StreamingMessage
        {
            InvocationResponse = InvocationResponseCaptureTests.SuccessResponse("id")
        });
        AssertCaptureReleased(service, "id");
    }

    [Fact]
    public async Task SendAsync_ForwardingFailureReleasesCapture()
    {
        var service = InvocationResponseCaptureTests.CreateService();
        using var inner = new CallbackHandler((_, _) => throw new HttpRequestException("forwarding failed"));
        using var handler = new AspNetCoreForwardingHandler(inner, service);
        using var client = new HttpClient(handler);
        using var request = CreateRequest();
        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(request, TestContext.Current.CancellationToken));
        AssertCaptureReleased(service, "id");
    }

    [Fact]
    public async Task SendAsync_MultipleInvocationIdHeaderValues_ThrowsInsteadOfMangledKey()
    {
        var service = InvocationResponseCaptureTests.CreateService();
        using var inner = new CallbackHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var handler = new AspNetCoreForwardingHandler(inner, service);
        using var client = new HttpClient(handler);
        using var request = CreateRequest();
        request.Headers.Add("x-ms-invocation-id", "another-id");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SendAsync(request, TestContext.Current.CancellationToken));
    }

    private static HttpRequestMessage CreateRequest(string path = "/api/items")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"http://localhost{path}");
        request.Headers.Add("x-ms-invocation-id", "id");
        return request;
    }

    private static void AssertCaptureReleased(AzureFunctions.TestFramework.Core.Grpc.GrpcHostService service, string id)
    {
        using var capture = service.BeginCaptureInvocationResponse(id);
        Assert.NotNull(capture);
    }

    private sealed class CallbackHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }

    private sealed class CallbackContent(Func<Task> onSerialize) : HttpContent
    {
        public bool Serialized { get; private set; }
        public bool Disposed { get; private set; }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            Serialized = true;
            await stream.WriteAsync(Encoding.UTF8.GetBytes("body"));
            await onSerialize();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
