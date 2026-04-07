using System.Text.Json;
using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Durable;

/// <summary>
/// Injects a synthetic <c>durableClient</c> input binding parameter into every invocation of
/// functions that declare a <c>[DurableClient] DurableTaskClient</c> parameter.
/// <para>
/// The real Azure Functions host serialises a JSON payload (<c>rpcBaseUrl</c>,
/// <c>taskHubName</c>, <c>connectionName</c>, …) into the <c>durableClient</c> binding slot
/// so that the worker's <c>DurableTaskClientConverter</c> can construct the client object.
/// This provider synthesises an equivalent payload using the fake gRPC/HTTP endpoints
/// defined in <see cref="DurableClientBindingDefaults"/>.
/// </para>
/// <para>
/// Register via <see cref="IFunctionsTestHostBuilder.WithSyntheticBindingProvider"/>, which
/// <see cref="FunctionsTestHostBuilderDurableExtensions.ConfigureFakeDurableSupport"/> does
/// automatically.
/// </para>
/// </summary>
public sealed class DurableClientSyntheticBindingProvider : ISyntheticBindingProvider
{
    /// <inheritdoc/>
    public string BindingType => "durableClient";

    /// <inheritdoc/>
    public FunctionBindingData CreateSyntheticParameter(string parameterName, JsonElement bindingConfig)
    {
        var taskHub = bindingConfig.TryGetProperty("taskHub", out var th) ? th.GetString() : null;
        var connectionName = bindingConfig.TryGetProperty("connectionName", out var cn) ? cn.GetString() : null;

        return FunctionBindingData.WithString(
            parameterName,
            DurableClientBindingDefaults.CreatePayload(taskHub, connectionName));
    }
}
