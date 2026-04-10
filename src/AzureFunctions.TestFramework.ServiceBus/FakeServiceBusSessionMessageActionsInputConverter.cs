using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzureFunctions.TestFramework.ServiceBus;

/// <summary>
/// Input converter that resolves <see cref="ServiceBusSessionMessageActions"/> binding parameters to the
/// <see cref="FakeServiceBusSessionMessageActions"/> singleton registered in the worker's DI container.
/// </summary>
/// <remarks>
/// This converter is registered as the <c>ServiceBusSessionMessageActionsConverter</c> implementation
/// in DI by <see cref="FunctionsTestHostBuilderServiceBusExtensions.ConfigureFakeServiceBusMessageActions"/>,
/// intercepting the SDK's settlement-gRPC converter which requires a real Service Bus connection.
/// </remarks>
[SupportedTargetType(typeof(ServiceBusSessionMessageActions))]
public sealed class FakeServiceBusSessionMessageActionsInputConverter : IInputConverter
{
    private readonly FakeServiceBusSessionMessageActions _actions;
    private readonly ILogger<FakeServiceBusSessionMessageActionsInputConverter> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="FakeServiceBusSessionMessageActionsInputConverter"/>.
    /// </summary>
    public FakeServiceBusSessionMessageActionsInputConverter(IServiceProvider serviceProvider)
    {
        _actions = serviceProvider.GetRequiredService<FakeServiceBusSessionMessageActions>();
        _logger = serviceProvider.GetRequiredService<ILogger<FakeServiceBusSessionMessageActionsInputConverter>>();
    }

    /// <inheritdoc />
    public ValueTask<ConversionResult> ConvertAsync(ConverterContext context)
    {
        // Use FullName-based comparison as defense-in-depth against ALC type-identity mismatches.
        if (!context.TargetType.IsAssignableFrom(typeof(FakeServiceBusSessionMessageActions))
            && context.TargetType.FullName != typeof(ServiceBusSessionMessageActions).FullName)
        {
            return ValueTask.FromResult(ConversionResult.Unhandled());
        }

        _logger.LogDebug("Injecting FakeServiceBusSessionMessageActions for target type {TargetType}", context.TargetType);
        return ValueTask.FromResult(ConversionResult.Success(_actions));
    }
}
