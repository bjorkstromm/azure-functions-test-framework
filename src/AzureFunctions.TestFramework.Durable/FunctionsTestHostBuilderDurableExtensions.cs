using System.Reflection;
using AzureFunctions.TestFramework.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AzureFunctions.TestFramework.Durable;

/// <summary>
/// Durable-specific extensions for <see cref="IFunctionsTestHostBuilder"/>.
/// </summary>
public static class FunctionsTestHostBuilderDurableExtensions
{
    /// <summary>
    /// Registers fake durable services that allow isolated-worker starter/orchestrator/activity flows
    /// to run fully in-process under <see cref="FunctionsTestHost"/>.
    /// </summary>
    /// <param name="builder">The test host builder.</param>
    /// <param name="functionsAssembly">The functions assembly containing durable functions.</param>
    /// <returns>The same builder instance.</returns>
    public static IFunctionsTestHostBuilder ConfigureFakeDurableSupport(
        this IFunctionsTestHostBuilder builder,
        Assembly functionsAssembly)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(functionsAssembly);

        return builder.ConfigureServices(services =>
        {
            services.TryAddSingleton(new FakeDurableFunctionCatalog(functionsAssembly));
            services.TryAddSingleton<FakeDurableOrchestrationRunner>();
            services.TryAddSingleton<FakeDurableTaskClient>();
            services.TryAddSingleton<DurableTaskClient>(provider => provider.GetRequiredService<FakeDurableTaskClient>());
            services.TryAddSingleton<FunctionsDurableClientProvider>();
            services.TryAddSingleton<FakeDurableTaskClientInputConverter>();

            services.AddOptions<WorkerOptions>().Configure(options =>
            {
                if (!options.InputConverters.Contains(typeof(FakeDurableTaskClientInputConverter)))
                {
                    options.InputConverters.RegisterAt<FakeDurableTaskClientInputConverter>(0);
                }
            });
        });
    }
}
