using System.Text.Json;

namespace AzureFunctions.TestFramework.Durable;

/// <summary>
/// HTTP helpers for Durable Functions starter and status endpoints.
/// </summary>
public static class DurableHttpClientExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Reads a Durable HTTP management payload from a response body.
    /// </summary>
    /// <param name="response">The HTTP response to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized payload, or <see langword="null"/> when the response body is empty.</returns>
    public static async Task<DurableHttpManagementPayload?> ReadDurableHttpManagementPayloadAsync(
        this HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        if (stream.Length == 0)
        {
            return null;
        }

        return await JsonSerializer.DeserializeAsync<DurableHttpManagementPayload>(
            stream,
            _jsonOptions,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads a Durable orchestration status document from a response body.
    /// </summary>
    /// <param name="response">The HTTP response to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized status document, or <see langword="null"/> when the response body is empty.</returns>
    public static async Task<DurableOrchestrationStatus?> ReadDurableOrchestrationStatusAsync(
        this HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        if (stream.Length == 0)
        {
            return null;
        }

        return await JsonSerializer.DeserializeAsync<DurableOrchestrationStatus>(
            stream,
            _jsonOptions,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Polls a Durable orchestration status endpoint until a terminal state is reached or a timeout expires.
    /// </summary>
    /// <param name="client">The HTTP client used to query the status URL.</param>
    /// <param name="payload">The management payload that contains the status query URL.</param>
    /// <param name="timeout">The maximum amount of time to wait for completion.</param>
    /// <param name="pollInterval">Optional delay between polls. Defaults to 200 ms.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The terminal orchestration status.</returns>
    public static async Task<DurableOrchestrationStatus> WaitForCompletionAsync(
        this HttpClient client,
        DurableHttpManagementPayload payload,
        TimeSpan timeout,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(payload);

        if (string.IsNullOrWhiteSpace(payload.StatusQueryGetUri))
        {
            throw new InvalidOperationException("The Durable management payload does not include a statusQueryGetUri.");
        }

        var interval = pollInterval ?? TimeSpan.FromMilliseconds(200);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        while (true)
        {
            using var response = await client.GetAsync(payload.StatusQueryGetUri, timeoutCts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var status = await response.ReadDurableOrchestrationStatusAsync(timeoutCts.Token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The Durable status endpoint returned an empty response.");

            if (status.IsTerminal)
            {
                return status;
            }

            await Task.Delay(interval, timeoutCts.Token).ConfigureAwait(false);
        }
    }
}
