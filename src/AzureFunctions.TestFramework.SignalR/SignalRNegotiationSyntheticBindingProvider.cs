using System.Text.Json;
using AzureFunctions.TestFramework.Core;
using Microsoft.Azure.Functions.Worker;

namespace AzureFunctions.TestFramework.SignalR;

/// <summary>
/// Injects synthetic <c>signalRNegotiation</c> input binding data into every invocation of
/// functions that declare a <c>[SignalRNegotiationInput]</c> parameter.
/// <para>
/// The real Azure Functions host calls the SignalR service to perform negotiation and passes
/// a <see cref="SignalRNegotiationContext"/> as JSON in the <c>InputData</c>.
/// This provider injects a pre-configured context so that the worker can construct the target
/// type without connecting to a real SignalR service.
/// </para>
/// <para>
/// Register via
/// <see cref="FunctionsTestHostBuilderSignalRExtensions.WithSignalRNegotiation(IFunctionsTestHostBuilder, SignalRNegotiationContext)"/>.
/// </para>
/// </summary>
public sealed class SignalRNegotiationSyntheticBindingProvider : ISyntheticBindingProvider
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _negotiationContextJson;

    /// <summary>
    /// Initialises a new instance with a pre-configured <see cref="SignalRNegotiationContext"/>.
    /// </summary>
    /// <param name="negotiationContext">The negotiation context to inject for every <c>signalRNegotiation</c> binding.</param>
    public SignalRNegotiationSyntheticBindingProvider(SignalRNegotiationContext negotiationContext)
    {
        ArgumentNullException.ThrowIfNull(negotiationContext);
        _negotiationContextJson = JsonSerializer.Serialize(negotiationContext, _jsonOptions);
    }

    /// <inheritdoc/>
    public string BindingType => "signalRNegotiation";

    /// <inheritdoc/>
    public FunctionBindingData CreateSyntheticParameter(string parameterName, JsonElement bindingConfig)
        => FunctionBindingData.WithJson(parameterName, _negotiationContextJson);
}
