using Microsoft.DurableTask.Client;

namespace AzureFunctions.TestFramework.Durable;

/// <summary>
/// Provides access to the fake-backed <see cref="DurableTaskClient"/> registered by
/// <see cref="FunctionsTestHostBuilderDurableExtensions.ConfigureFakeDurableSupport(AzureFunctions.TestFramework.Core.IFunctionsTestHostBuilder,System.Reflection.Assembly)"/>.
/// </summary>
public sealed class FunctionsDurableClientProvider
{
    private readonly DurableTaskClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionsDurableClientProvider"/> class.
    /// </summary>
    /// <param name="client">The fake-backed durable client to expose to tests.</param>
    public FunctionsDurableClientProvider(DurableTaskClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Gets the fake-backed durable client instance.
    /// </summary>
    public DurableTaskClient Client => _client;

    /// <summary>
    /// Gets the fake-backed durable client instance.
    /// </summary>
    /// <returns>The configured durable client.</returns>
    public DurableTaskClient GetClient() => _client;
}
