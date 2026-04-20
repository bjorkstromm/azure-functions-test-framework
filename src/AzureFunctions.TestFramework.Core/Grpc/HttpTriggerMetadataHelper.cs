using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using System.Text.Json;

namespace AzureFunctions.TestFramework.Core.Grpc;

/// <summary>
/// Populates <c>TriggerMetadata</c> in an <see cref="InvocationRequest"/> to match the real Azure
/// Functions host behavior for HTTP triggers:
/// <list type="bullet">
///   <item><description>Top-level JSON body scalars (string / number / bool) → <c>TypedData.String</c></description></item>
///   <item><description>Top-level JSON body objects → <c>TypedData.Json</c></description></item>
///   <item><description>Top-level JSON body arrays → <b>skipped</b> (matches real host)</description></item>
///   <item><description><c>Query</c> → serialized query-param dictionary as <c>TypedData.Json</c></description></item>
///   <item><description><c>Headers</c> → serialized headers dictionary as <c>TypedData.Json</c></description></item>
/// </list>
/// This makes <c>FunctionContext.BindingContext.BindingData</c> available inside the function
/// exactly as it would be when running against a real Azure Functions host.
/// </summary>
public static class HttpTriggerMetadataHelper
{
    private static readonly JsonSerializerOptions s_jsonOptions = new();

    /// <summary>
    /// Populates the <paramref name="triggerMetadata"/> map with HTTP-trigger binding data derived
    /// from the request headers, query parameters, and body.
    /// </summary>
    /// <param name="triggerMetadata">The map to populate (from <see cref="InvocationRequest.TriggerMetadata"/>).</param>
    /// <param name="headers">HTTP request headers (may be <see langword="null"/>).</param>
    /// <param name="queryParams">HTTP query-string parameters (may be <see langword="null"/>).</param>
    /// <param name="body">Raw request body string (may be <see langword="null"/> or empty).</param>
    /// <param name="contentType">Value of the <c>Content-Type</c> header (used to detect JSON bodies).</param>
    public static void PopulateTriggerMetadata(
        Google.Protobuf.Collections.MapField<string, TypedData> triggerMetadata,
        IReadOnlyDictionary<string, string>? headers,
        IReadOnlyDictionary<string, string>? queryParams,
        string? body,
        string? contentType)
    {
        // Headers → JSON object
        var headerDict = headers ?? new Dictionary<string, string>();
        triggerMetadata["Headers"] = new TypedData
        {
            Json = JsonSerializer.Serialize(headerDict, s_jsonOptions)
        };

        // Query → JSON object
        var queryDict = queryParams ?? new Dictionary<string, string>();
        triggerMetadata["Query"] = new TypedData
        {
            Json = JsonSerializer.Serialize(queryDict, s_jsonOptions)
        };

        // JSON body → individual top-level properties
        if (!string.IsNullOrEmpty(body) && IsJsonContentType(contentType))
        {
            TryPopulateBodyProperties(triggerMetadata, body);
        }
    }

    private static void TryPopulateBodyProperties(
        Google.Protobuf.Collections.MapField<string, TypedData> triggerMetadata,
        string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                switch (prop.Value.ValueKind)
                {
                    case JsonValueKind.String:
                        triggerMetadata[prop.Name] = new TypedData
                        {
                            String = prop.Value.GetString() ?? string.Empty
                        };
                        break;

                    case JsonValueKind.Number:
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        // Scalars stored as raw JSON text (e.g. "1312", "true") — matches real host.
                        triggerMetadata[prop.Name] = new TypedData
                        {
                            String = prop.Value.GetRawText()
                        };
                        break;

                    case JsonValueKind.Object:
                        triggerMetadata[prop.Name] = new TypedData
                        {
                            Json = prop.Value.GetRawText()
                        };
                        break;

                    // Arrays are intentionally excluded to match real Azure Functions host behavior.
                }
            }
        }
        catch (JsonException)
        {
            // Body is not valid JSON — silently skip body property population.
        }
    }

    private static bool IsJsonContentType(string? contentType)
        => contentType != null &&
           (contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) ||
            contentType.StartsWith("text/json", StringComparison.OrdinalIgnoreCase));
}
