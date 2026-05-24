using System.Net;
using System.Text;
using System.Text.Json;
using AzureFunctions.TestFramework.Durable;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Durable;

/// <summary>
/// Unit tests for <see cref="DurableHttpClientExtensions.WaitForCompletionAsync"/>
/// and <see cref="DurableHttpClientExtensions.ReadDurableOrchestrationStatusAsync"/>.
/// </summary>
public class DurableHttpClientExtensionsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // -------------------------------------------------------------------------
    // ReadDurableOrchestrationStatusAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ReadDurableOrchestrationStatusAsync_EmptyBody_ReturnsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Array.Empty<byte>())
        };
        var result = await response.ReadDurableOrchestrationStatusAsync(TestContext.Current.CancellationToken);
        Assert.Null(result);
    }

    [Fact]
    public async Task ReadDurableOrchestrationStatusAsync_ValidJson_DeserializesStatus()
    {
        var json = JsonSerializer.Serialize(new
        {
            name = "MyOrchestrator",
            instanceId = "abc123",
            runtimeStatus = "Completed"
        });
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var result = await response.ReadDurableOrchestrationStatusAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("MyOrchestrator", result!.Name);
        Assert.Equal("abc123", result.InstanceId);
        Assert.Equal("Completed", result.RuntimeStatus);
    }

    [Fact]
    public async Task ReadDurableOrchestrationStatusAsync_NullResponse_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ((HttpResponseMessage)null!).ReadDurableOrchestrationStatusAsync());
    }

    // -------------------------------------------------------------------------
    // WaitForCompletionAsync — validation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task WaitForCompletionAsync_NullClient_Throws()
    {
        var payload = new DurableHttpManagementPayload { StatusQueryGetUri = "http://example.com/status" };
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ((HttpClient)null!).WaitForCompletionAsync(payload, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaitForCompletionAsync_NullPayload_Throws()
    {
        using var client = new HttpClient();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.WaitForCompletionAsync(null!, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaitForCompletionAsync_EmptyStatusUri_Throws()
    {
        using var client = new HttpClient();
        var payload = new DurableHttpManagementPayload { StatusQueryGetUri = "" };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.WaitForCompletionAsync(payload, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaitForCompletionAsync_WhitespaceStatusUri_Throws()
    {
        using var client = new HttpClient();
        var payload = new DurableHttpManagementPayload { StatusQueryGetUri = "   " };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.WaitForCompletionAsync(payload, TimeSpan.FromSeconds(5)));
    }

    // -------------------------------------------------------------------------
    // WaitForCompletionAsync — polling behaviour
    // -------------------------------------------------------------------------

    [Fact]
    public async Task WaitForCompletionAsync_ImmediateTerminalStatus_ReturnsStatus()
    {
        var statusJson = JsonSerializer.Serialize(new { runtimeStatus = "Completed", instanceId = "inst1" });
        var handler = new SingleResponseHandler(statusJson);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        var payload = new DurableHttpManagementPayload { StatusQueryGetUri = "http://localhost/status" };
        var result = await client.WaitForCompletionAsync(
            payload,
            timeout: TimeSpan.FromSeconds(5),
            pollInterval: TimeSpan.FromMilliseconds(10),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Completed", result.RuntimeStatus);
        Assert.Equal("inst1", result.InstanceId);
    }

    [Fact]
    public async Task WaitForCompletionAsync_RunningThenCompleted_ReturnsFinalStatus()
    {
        // First poll returns Running, second poll returns Completed.
        var responses = new Queue<string>([
            JsonSerializer.Serialize(new { runtimeStatus = "Running" }),
            JsonSerializer.Serialize(new { runtimeStatus = "Completed", instanceId = "done" })
        ]);
        var handler = new QueuedResponseHandler(responses);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        var payload = new DurableHttpManagementPayload { StatusQueryGetUri = "http://localhost/status" };
        var result = await client.WaitForCompletionAsync(
            payload,
            timeout: TimeSpan.FromSeconds(5),
            pollInterval: TimeSpan.FromMilliseconds(10),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Completed", result.RuntimeStatus);
        Assert.Equal("done", result.InstanceId);
    }

    [Fact]
    public async Task WaitForCompletionAsync_FailedStatus_ReturnsFailedStatus()
    {
        var statusJson = JsonSerializer.Serialize(new { runtimeStatus = "Failed", instanceId = "fail1" });
        var handler = new SingleResponseHandler(statusJson);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        var payload = new DurableHttpManagementPayload { StatusQueryGetUri = "http://localhost/status" };
        var result = await client.WaitForCompletionAsync(
            payload,
            timeout: TimeSpan.FromSeconds(5),
            pollInterval: TimeSpan.FromMilliseconds(10),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Failed", result.RuntimeStatus);
    }

    [Fact]
    public async Task WaitForCompletionAsync_Timeout_ThrowsOperationCanceled()
    {
        // Always returns Running — should time out.
        var handler = new SingleResponseHandler(
            JsonSerializer.Serialize(new { runtimeStatus = "Running" }),
            repeating: true);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        var payload = new DurableHttpManagementPayload { StatusQueryGetUri = "http://localhost/status" };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.WaitForCompletionAsync(
                payload,
                timeout: TimeSpan.FromMilliseconds(100),
                pollInterval: TimeSpan.FromMilliseconds(50)));
    }

    // -------------------------------------------------------------------------
    // Test helpers
    // -------------------------------------------------------------------------

    private sealed class SingleResponseHandler : HttpMessageHandler
    {
        private readonly string _responseJson;
        private readonly bool _repeating;

        public SingleResponseHandler(string responseJson, bool repeating = false)
        {
            _responseJson = responseJson;
            _repeating = repeating;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private sealed class QueuedResponseHandler : HttpMessageHandler
    {
        private readonly Queue<string> _responses;

        public QueuedResponseHandler(Queue<string> responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = _responses.Count > 0 ? _responses.Dequeue() : "{}";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
