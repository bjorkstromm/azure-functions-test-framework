using System.Reflection;
using AzureFunctions.TestFramework.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections;

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
            var durableBindingConfigurations = DiscoverDurableClientBindings(functionsAssembly);

            services.TryAddSingleton(new FakeDurableFunctionCatalog(functionsAssembly));
            services.TryAddSingleton<FakeDurableExternalEventHub>();
            services.TryAddSingleton<FakeDurableOrchestrationRunner>();
            services.TryAddSingleton<FakeDurableTaskClient>();
            services.TryAddSingleton<DurableTaskClient>(provider => provider.GetRequiredService<FakeDurableTaskClient>());
            services.TryAddSingleton<FunctionsDurableClientProvider>();

            // Register our fake input converter and alias it as the real DurableTaskClientConverter
            // type in DI. The SDK's DefaultInputConverterProvider.GetOrCreateConverterInstance calls
            // ActivatorUtilities.GetServiceOrCreateInstance(sp, converterType) which checks DI first.
            // By registering our fake for the real converter's type, the SDK resolves our fake instead
            // of creating the real converter — which would fail because it expects a JSON binding
            // payload (rpcBaseUrl, taskHubName, etc.) that is only present in the gRPC-direct path.
            services.TryAddSingleton<FakeDurableTaskClientInputConverter>();
            var realConverterType = typeof(DurableClientAttribute).Assembly.GetType(
                "Microsoft.Azure.Functions.Worker.Extensions.DurableTask.DurableTaskClientConverter",
                throwOnError: true)!;
            services.AddSingleton(realConverterType, sp => sp.GetRequiredService<FakeDurableTaskClientInputConverter>());

            RegisterInternalDurableClientProvider(services, durableBindingConfigurations);
        });
    }

    private static void RegisterInternalDurableClientProvider(
        IServiceCollection services,
        IReadOnlyCollection<(string TaskHub, string ConnectionName)> durableBindingConfigurations)
    {
        var providerType = typeof(DurableClientAttribute).Assembly.GetType(
            "Microsoft.Azure.Functions.Worker.Extensions.DurableTask.FunctionsDurableClientProvider",
            throwOnError: true)!;

        services.TryAddSingleton(providerType, serviceProvider =>
        {
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var options = serviceProvider.GetService<IOptions<DurableTaskClientOptions>>()
                ?? Options.Create(new DurableTaskClientOptions());

            var provider = Activator.CreateInstance(
                providerType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: [loggerFactory, options],
                culture: null)!;

            var clientsField = providerType.GetField("clients", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not find internal Durable client cache field.");
            var clients = clientsField.GetValue(provider) as IDictionary
                ?? throw new InvalidOperationException("Could not access internal Durable client cache.");

            var clientKeyType = providerType.GetNestedType("ClientKey", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not find internal Durable client key type.");
            var clientHolderType = providerType.GetNestedType("ClientHolder", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not find internal Durable client holder type.");

            var clientKeyConstructor = clientKeyType
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(ctor =>
                {
                    var parameters = ctor.GetParameters();
                    return parameters.Length == 3
                        && parameters[0].ParameterType == typeof(Uri)
                        && parameters[1].ParameterType == typeof(string)
                        && parameters[2].ParameterType == typeof(string);
                });
            var clientHolderConstructor = clientHolderType
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(ctor =>
                {
                    var parameters = ctor.GetParameters();
                    return parameters.Length == 2
                        && parameters[0].ParameterType == typeof(DurableTaskClient);
                });
            var fakeClient = serviceProvider.GetRequiredService<FakeDurableTaskClient>();
            var endpoint = new Uri(DurableClientBindingDefaults.RpcBaseUrl);

            foreach (var bindingConfiguration in durableBindingConfigurations)
            {
                var key = clientKeyConstructor.Invoke([endpoint, bindingConfiguration.TaskHub, bindingConfiguration.ConnectionName]);
                var holder = clientHolderConstructor.Invoke([fakeClient, null]);
                clients[key] = holder;
            }

            return provider;
        });
    }

    private static IReadOnlyCollection<(string TaskHub, string ConnectionName)> DiscoverDurableClientBindings(Assembly functionsAssembly)
    {
        HashSet<(string TaskHub, string ConnectionName)> bindings = [];

        foreach (var type in functionsAssembly.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                foreach (var parameter in method.GetParameters())
                {
                    var attribute = parameter.GetCustomAttribute<DurableClientAttribute>();
                    if (attribute == null || parameter.ParameterType != typeof(DurableTaskClient))
                    {
                        continue;
                    }

                    bindings.Add((attribute.TaskHub ?? string.Empty, attribute.ConnectionName ?? string.Empty));
                }
            }
        }

        if (bindings.Count == 0)
        {
            bindings.Add((string.Empty, string.Empty));
        }

        return bindings;
    }
}
