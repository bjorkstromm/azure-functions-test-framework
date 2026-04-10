using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzureFunctions.TestFramework.ServiceBus;

/// <summary>
/// Input converter that resolves <see cref="ServiceBusMessageActions"/> binding parameters to the
/// <see cref="FakeServiceBusMessageActions"/> singleton registered in the worker's DI container.
/// </summary>
/// <remarks>
/// This converter is registered as the <c>ServiceBusMessageActionsConverter</c> implementation in
/// DI by <see cref="FunctionsTestHostBuilderServiceBusExtensions.ConfigureFakeServiceBusMessageActions"/>,
/// intercepting the SDK's settlement-gRPC converter which requires a real Service Bus connection.
/// </remarks>
[SupportedTargetType(typeof(ServiceBusMessageActions))]
public sealed class FakeServiceBusMessageActionsInputConverter : IInputConverter
{
    private readonly FakeServiceBusMessageActions _actions;
    private readonly ILogger<FakeServiceBusMessageActionsInputConverter> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="FakeServiceBusMessageActionsInputConverter"/>.
    /// </summary>
    public FakeServiceBusMessageActionsInputConverter(IServiceProvider serviceProvider)
    {
        _actions = serviceProvider.GetRequiredService<FakeServiceBusMessageActions>();
        _logger = serviceProvider.GetRequiredService<ILogger<FakeServiceBusMessageActionsInputConverter>>();
    }

    /// <inheritdoc />
    public ValueTask<ConversionResult> ConvertAsync(ConverterContext context)
    {
        // Use FullName-based comparison as defense-in-depth against ALC type-identity mismatches.
        if (!context.TargetType.IsAssignableFrom(typeof(FakeServiceBusMessageActions))
            && context.TargetType.FullName != typeof(ServiceBusMessageActions).FullName)
        {
            return ValueTask.FromResult(ConversionResult.Unhandled());
        }

        _logger.LogDebug("Injecting FakeServiceBusMessageActions for target type {TargetType}", context.TargetType);
        return ValueTask.FromResult(ConversionResult.Success(_actions));
    }
}
