using System.Text.Json;
using AzureFunctions.TestFramework.Core;
using Microsoft.Azure.Functions.Worker;

namespace AzureFunctions.TestFramework.SignalR;

/// <summary>
/// Injects synthetic <c>signalREndpoints</c> input binding data into every invocation of
/// functions that declare a <c>[SignalREndpointsInput]</c> parameter.
/// <para>
/// The real Azure Functions host queries the SignalR service for its available endpoints and
/// passes them as JSON in the <c>InputData</c> of the <c>InvocationRequest</c>.
/// This provider injects a pre-configured <see cref="SignalREndpoint"/> array so that the
/// worker can construct the target type without connecting to a real SignalR service.
/// </para>
/// <para>
/// Register via
/// <see cref="FunctionsTestHostBuilderSignalRExtensions.WithSignalREndpoints(IFunctionsTestHostBuilder, SignalREndpoint[])"/>.
/// </para>
/// </summary>
public sealed class SignalREndpointsSyntheticBindingProvider : ISyntheticBindingProvider
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _endpointsJson;

    /// <summary>
    /// Initialises a new instance with the specified SignalR endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoints to inject for every <c>signalREndpoints</c> binding.</param>
    public SignalREndpointsSyntheticBindingProvider(SignalREndpoint[] endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        _endpointsJson = JsonSerializer.Serialize(endpoints, _jsonOptions);
    }

    /// <inheritdoc/>
    public string BindingType => "signalREndpoints";

    /// <inheritdoc/>
    public FunctionBindingData CreateSyntheticParameter(string parameterName, JsonElement bindingConfig)
        => FunctionBindingData.WithJson(parameterName, _endpointsJson);
}
