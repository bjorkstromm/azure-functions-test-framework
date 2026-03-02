namespace AzureFunctions.TestFramework.Core;

/// <summary>
/// Extension methods for IFunctionsTestHostBuilder.
/// </summary>
public static class FunctionsTestHostBuilderExtensions
{
    /// <summary>
    /// Builds the test host and starts it immediately.
    /// Convenience method that combines Build() and StartAsync().
    /// </summary>
    public static async Task<IFunctionsTestHost> BuildAndStartAsync(
        this IFunctionsTestHostBuilder builder,
        CancellationToken cancellationToken = default)
    {
        var host = builder.Build();
        await host.StartAsync(cancellationToken);
        return host;
    }
}
