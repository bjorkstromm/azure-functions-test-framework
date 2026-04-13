using System.Text.Json;
using AzureFunctions.TestFramework.Core;
using Microsoft.Azure.Functions.Worker;

namespace AzureFunctions.TestFramework.SignalR;

/// <summary>
/// Injects synthetic <c>signalRConnectionInfo</c> input binding data into every invocation of
/// functions that declare a <c>[SignalRConnectionInfoInput]</c> parameter.
/// <para>
/// The real Azure Functions host calls the SignalR service to generate a client access token
/// and passes it as JSON in the <c>InputData</c> of the <c>InvocationRequest</c>.
/// This provider injects a pre-configured <see cref="SignalRConnectionInfo"/> so that the
/// worker can construct the target type without connecting to a real SignalR service.
/// </para>
/// <para>
/// Register via
/// <see cref="FunctionsTestHostBuilderSignalRExtensions.WithSignalRConnectionInfo(IFunctionsTestHostBuilder, string, string)"/>.
/// </para>
/// </summary>
public sealed class SignalRConnectionInfoSyntheticBindingProvider : ISyntheticBindingProvider
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _connectionInfoJson;

    /// <summary>
    /// Initialises a new instance with a pre-serialized <see cref="SignalRConnectionInfo"/> JSON payload.
    /// </summary>
    /// <param name="connectionInfoJson">The JSON string to inject for every <c>signalRConnectionInfo</c> binding.</param>
    public SignalRConnectionInfoSyntheticBindingProvider(string connectionInfoJson)
    {
        ArgumentNullException.ThrowIfNull(connectionInfoJson);
        _connectionInfoJson = connectionInfoJson;
    }

    /// <summary>
    /// Initialises a new instance with the specified URL and access token.
    /// </summary>
    /// <param name="url">The SignalR service client endpoint URL.</param>
    /// <param name="accessToken">The SignalR service access token.</param>
    public SignalRConnectionInfoSyntheticBindingProvider(string url, string accessToken)
        : this(JsonSerializer.Serialize(
            new SignalRConnectionInfo { Url = url, AccessToken = accessToken },
            _jsonOptions))
    {
    }

    /// <inheritdoc/>
    public string BindingType => "signalRConnectionInfo";

    /// <inheritdoc/>
    public FunctionBindingData CreateSyntheticParameter(string parameterName, JsonElement bindingConfig)
        => FunctionBindingData.WithJson(parameterName, _connectionInfoJson);
}
