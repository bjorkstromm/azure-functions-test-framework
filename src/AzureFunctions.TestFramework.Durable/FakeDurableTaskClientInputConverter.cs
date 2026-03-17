using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzureFunctions.TestFramework.Durable;

[SupportedTargetType(typeof(DurableTaskClient))]
public sealed class FakeDurableTaskClientInputConverter : IInputConverter
{
    private readonly FakeDurableTaskClient _client;
    private readonly ILogger<FakeDurableTaskClientInputConverter> _logger;

    public FakeDurableTaskClientInputConverter(IServiceProvider serviceProvider)
    {
        _client = serviceProvider.GetRequiredService<FakeDurableTaskClient>();
        _logger = serviceProvider.GetRequiredService<ILogger<FakeDurableTaskClientInputConverter>>();
    }

    public ValueTask<ConversionResult> ConvertAsync(ConverterContext context)
    {
        if (!context.TargetType.IsAssignableFrom(typeof(FakeDurableTaskClient)))
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
