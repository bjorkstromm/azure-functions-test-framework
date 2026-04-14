using System.Text.Json;

namespace AzureFunctions.TestFramework.Core;

/// <summary>
/// Extension point for non-trigger input bindings that must be injected as synthetic
/// <c>ParameterBinding</c> entries into every invocation of a function that declares
/// the corresponding binding attribute.
/// <para>
/// The canonical example is <c>[DurableClient]</c>: the real Azure Functions host serialises
/// a JSON payload (rpcBaseUrl, taskHubName, …) into the <c>durableClient</c> binding slot so
/// the worker's <c>DurableTaskClientConverter</c> can construct the client object.
/// The <c>AzureFunctions.TestFramework.Durable</c> package registers an implementation for
/// <c>"durableClient"</c> that provides an equivalent synthetic payload.
/// </para>
/// <para>
/// Register an implementation via
/// <see cref="IFunctionsTestHostBuilder.WithSyntheticBindingProvider"/>.
/// </para>
/// </summary>
public interface ISyntheticBindingProvider
{
    /// <summary>
    /// Gets the binding type string this provider handles (e.g. <c>"durableClient"</c>).
    /// Must match the <c>"type"</c> field in the function's raw binding JSON.
    /// </summary>
    string BindingType { get; }

    /// <summary>
    /// Creates the synthetic binding parameter to inject into the invocation request.
    /// Return <see langword="null"/> to skip this binding (e.g. when the path or direction
    /// does not match this provider's scope).
    /// </summary>
    /// <param name="parameterName">
    /// The parameter name for this binding as declared in the function signature.
    /// </param>
    /// <param name="bindingConfig">
    /// The full binding JSON element from the function's source-generated metadata
    /// (contains fields such as <c>taskHub</c>, <c>connectionName</c>, etc.).
    /// </param>
    FunctionBindingData? CreateSyntheticParameter(string parameterName, JsonElement bindingConfig);
}
