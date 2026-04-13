using AzureFunctions.TestFramework.Core;
using Microsoft.Azure.Functions.Worker;

namespace AzureFunctions.TestFramework.SignalR;

/// <summary>
/// Extension methods on <see cref="IFunctionsTestHostBuilder"/> for configuring
/// SignalR input binding support.
/// </summary>
public static class FunctionsTestHostBuilderSignalRExtensions
{
    /// <summary>
    /// Registers a synthetic <c>signalRConnectionInfo</c> binding that injects the specified
    /// URL and access token for every function invocation that declares a
    /// <c>[SignalRConnectionInfoInput]</c> parameter.
    /// </summary>
    /// <param name="builder">The test host builder.</param>
    /// <param name="url">The SignalR service client endpoint URL to inject.</param>
    /// <param name="accessToken">The SignalR service access token to inject.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFunctionsTestHostBuilder WithSignalRConnectionInfo(
        this IFunctionsTestHostBuilder builder,
        string url,
        string accessToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(accessToken);

        return builder.WithSyntheticBindingProvider(
            new SignalRConnectionInfoSyntheticBindingProvider(url, accessToken));
    }

    /// <summary>
    /// Registers a synthetic <c>signalRConnectionInfo</c> binding that injects the specified
    /// <see cref="SignalRConnectionInfo"/> for every function invocation that declares a
    /// <c>[SignalRConnectionInfoInput]</c> parameter.
    /// </summary>
    /// <param name="builder">The test host builder.</param>
    /// <param name="connectionInfo">The connection info to inject.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFunctionsTestHostBuilder WithSignalRConnectionInfo(
        this IFunctionsTestHostBuilder builder,
        SignalRConnectionInfo connectionInfo)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(connectionInfo);

        return builder.WithSyntheticBindingProvider(
            new SignalRConnectionInfoSyntheticBindingProvider(
                connectionInfo.Url ?? string.Empty,
                connectionInfo.AccessToken ?? string.Empty));
    }

    /// <summary>
    /// Registers a synthetic <c>signalRNegotiation</c> binding that injects the specified
    /// <see cref="SignalRNegotiationContext"/> for every function invocation that declares a
    /// <c>[SignalRNegotiationInput]</c> parameter.
    /// </summary>
    /// <param name="builder">The test host builder.</param>
    /// <param name="negotiationContext">The negotiation context to inject.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFunctionsTestHostBuilder WithSignalRNegotiation(
        this IFunctionsTestHostBuilder builder,
        SignalRNegotiationContext negotiationContext)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(negotiationContext);

        return builder.WithSyntheticBindingProvider(
            new SignalRNegotiationSyntheticBindingProvider(negotiationContext));
    }

    /// <summary>
    /// Registers a synthetic <c>signalREndpoints</c> binding that injects the specified
    /// <see cref="SignalREndpoint"/> array for every function invocation that declares a
    /// <c>[SignalREndpointsInput]</c> parameter.
    /// </summary>
    /// <param name="builder">The test host builder.</param>
    /// <param name="endpoints">The SignalR endpoints to inject.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFunctionsTestHostBuilder WithSignalREndpoints(
        this IFunctionsTestHostBuilder builder,
        params SignalREndpoint[] endpoints)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(endpoints);

        return builder.WithSyntheticBindingProvider(
            new SignalREndpointsSyntheticBindingProvider(endpoints));
    }
}
