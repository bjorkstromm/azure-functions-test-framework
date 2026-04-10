using AzureFunctions.TestFramework.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AzureFunctions.TestFramework.ServiceBus;

/// <summary>
/// Service Bus–specific extensions for <see cref="IFunctionsTestHostBuilder"/>.
/// </summary>
public static class FunctionsTestHostBuilderServiceBusExtensions
{
    /// <summary>
    /// Registers fake <see cref="ServiceBusMessageActions"/> and
    /// <see cref="ServiceBusSessionMessageActions"/> implementations so that functions with
    /// those SDK-injected parameters can be invoked without a real Azure Service Bus settlement
    /// endpoint.
    /// </summary>
    /// <remarks>
    /// After calling this method the worker's DI container will contain:
    /// <list type="bullet">
    ///   <item><see cref="FakeServiceBusMessageActions"/> — a singleton that records all settlement calls.</item>
    ///   <item><see cref="FakeServiceBusSessionMessageActions"/> — a singleton that records all session calls.</item>
    /// </list>
    /// Both types are also registered under the real SDK converter types so the worker's
    /// <c>ActivatorUtilities.GetServiceOrCreateInstance</c> returns the fakes instead of creating
    /// the real converters (which require a live gRPC settlement channel).
    /// </remarks>
    /// <param name="builder">The test host builder.</param>
    /// <returns>The same builder instance for chaining.</returns>
    public static IFunctionsTestHostBuilder ConfigureFakeServiceBusMessageActions(
        this IFunctionsTestHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.ConfigureServices(services =>
        {
            services.TryAddSingleton<FakeServiceBusMessageActions>();
            services.TryAddSingleton<ServiceBusMessageActions>(
                sp => sp.GetRequiredService<FakeServiceBusMessageActions>());

            services.TryAddSingleton<FakeServiceBusSessionMessageActions>();
            services.TryAddSingleton<ServiceBusSessionMessageActions>(
                sp => sp.GetRequiredService<FakeServiceBusSessionMessageActions>());

            services.TryAddSingleton<FakeServiceBusMessageActionsInputConverter>();
            services.TryAddSingleton<FakeServiceBusSessionMessageActionsInputConverter>();

            // Register our fake input converters as the real SDK converter types so that the
            // SDK's ActivatorUtilities.GetServiceOrCreateInstance checks DI first and returns
            // our fakes instead of trying to create the real converters (which require a live
            // gRPC settlement channel).
            var sbAssembly = typeof(ServiceBusTriggerAttribute).Assembly;

            var realActionsConverterType = sbAssembly.GetType(
                "Microsoft.Azure.Functions.Worker.ServiceBusMessageActionsConverter",
                throwOnError: true)!;
            services.AddSingleton(realActionsConverterType,
                sp => sp.GetRequiredService<FakeServiceBusMessageActionsInputConverter>());

            var realSessionConverterType = sbAssembly.GetType(
                "Microsoft.Azure.Functions.Worker.ServiceBusSessionMessageActionsConverter",
                throwOnError: true)!;
            services.AddSingleton(realSessionConverterType,
                sp => sp.GetRequiredService<FakeServiceBusSessionMessageActionsInputConverter>());
        });
    }
}
