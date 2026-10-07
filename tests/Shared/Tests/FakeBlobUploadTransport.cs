using System.Net;
using Azure.Core.Pipeline;

namespace TestProject;

/// <summary>
/// A no-network <see cref="HttpClientTransport"/> that answers blob-upload requests (PUT) with a
/// canned 201 Created response and records each uploaded blob so tests can assert on it without
/// requiring Azurite or any real storage account.
/// </summary>
public sealed class FakeBlobUploadTransport : HttpClientTransport
{
    private readonly List<(string BlobName, string Content)> _uploads = [];

    public FakeBlobUploadTransport() : this([]) { }

    private FakeBlobUploadTransport(List<(string BlobName, string Content)> uploads)
        : base(new HttpClient(new RecordingHandler(uploads)))
    {
        _uploads = uploads;
    }

    /// <summary>Blob names and their uploaded content, in upload order.</summary>
    public IReadOnlyList<(string BlobName, string Content)> Uploads => _uploads;

    private sealed class RecordingHandler(List<(string BlobName, string Content)> uploads) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Put)
            {
                var content = request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken);
                var blobName = request.RequestUri!.Segments[^1];
                uploads.Add((blobName, content));

                var responseContent = new ByteArrayContent([]);
                responseContent.Headers.LastModified = DateTimeOffset.UtcNow;

                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    RequestMessage = request,
                    Content = responseContent,
                    Headers =
                    {
                        { "ETag", "\"0x8D0000000000000\"" },
                        { "x-ms-request-id", Guid.NewGuid().ToString() },
                        { "x-ms-version", "2024-08-04" },
                        { "x-ms-request-server-encrypted", "true" },
                    }
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request };
        }
    }
}
