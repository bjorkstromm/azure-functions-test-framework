using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzureFunctions.TestFramework.Durable;

/// <summary>
/// Converts durable-client input bindings into the framework's fake-backed
/// <see cref="DurableTaskClient"/> implementation for tests.
/// </summary>
[SupportedTargetType(typeof(DurableTaskClient))]
public sealed class FakeDurableTaskClientInputConverter : IInputConverter
{
    private readonly FakeDurableTaskClient _client;
    private readonly ILogger<FakeDurableTaskClientInputConverter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeDurableTaskClientInputConverter"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve the fake durable client and logger.</param>
    public FakeDurableTaskClientInputConverter(IServiceProvider serviceProvider)
    {
        _client = serviceProvider.GetRequiredService<FakeDurableTaskClient>();
        _logger = serviceProvider.GetRequiredService<ILogger<FakeDurableTaskClientInputConverter>>();
    }

    /// <summary>
    /// Converts the incoming binding context into a fake durable client when the target type
    /// and binding metadata match a durable-client parameter.
    /// </summary>
    /// <param name="context">The converter context for the current binding.</param>
    /// <returns>The conversion result for the requested input binding.</returns>
    public ValueTask<ConversionResult> ConvertAsync(ConverterContext context)
    {
        // Use FullName-based comparison as defense-in-depth against ALC type-identity mismatches
        // (same pattern as TestFunctionContextConverter). When the functions assembly is loaded
        // twice, typeof(DurableTaskClient) from the test runner's ALC may differ from the one
        // in context.TargetType, causing IsAssignableFrom to return false despite identical types.
        if (!context.TargetType.IsAssignableFrom(typeof(FakeDurableTaskClient))
            && context.TargetType.FullName != typeof(DurableTaskClient).FullName)
        {
            return ValueTask.FromResult(ConversionResult.Unhandled());
        }

        if (!context.TryGetBindingAttribute<DurableClientAttribute>(out _))
        {
            return ValueTask.FromResult(ConversionResult.Unhandled());
        }

        _logger.LogInformation("Using fake durable client input converter for target type {TargetType}", context.TargetType);
        return ValueTask.FromResult(ConversionResult.Success(_client));
    }
}
