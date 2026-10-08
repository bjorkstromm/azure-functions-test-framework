using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Dapr;
using AzureFunctions.TestFramework.RabbitMQ;
using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Dapr;

public class RawTriggerPayloadTests
{
#pragma warning disable xUnit1051 // Intentionally exercise overload resolution without a cancellation token.
    [Theory]
    [InlineData("binding", false)]
    [InlineData("binding", true)]
    [InlineData("invocation", false)]
    [InlineData("invocation", true)]
    [InlineData("topic", false)]
    [InlineData("topic", true)]
    [InlineData("rabbit", true)]
    [InlineData("rabbitMetadata", true)]
    [InlineData("rabbitNullMetadata", true)]
    public async Task RawOverloads_WithoutCancellationToken_DoNotSerializeJson(string trigger, bool binaryData)
    {
        using var host = new CapturingHost();
        byte[] bytes = [0, 255, 128, 195, 40, 0];
        var body = new BinaryData(bytes);
        var metadata = new RabbitMqTriggerMessageProperties { RoutingKey = "binary.route", MessageId = "binary-id" };

        _ = (trigger, binaryData) switch
        {
            ("binding", false) => await host.InvokeDaprBindingAsync("Function", bytes),
            ("binding", true) => await host.InvokeDaprBindingAsync("Function", body),
            ("invocation", false) => await host.InvokeDaprServiceInvocationAsync("Function", bytes),
            ("invocation", true) => await host.InvokeDaprServiceInvocationAsync("Function", body),
            ("topic", false) => await host.InvokeDaprTopicAsync("Function", bytes),
            ("topic", true) => await host.InvokeDaprTopicAsync("Function", body),
            ("rabbit", _) => await host.InvokeRabbitMQAsync("Function", body),
            ("rabbitNullMetadata", _) => await host.InvokeRabbitMQAsync("Function", body, messageProperties: null),
            _ => await host.InvokeRabbitMQAsync("Function", body, metadata)
        };

        var binding = Assert.Single(host.BindingData!.InputData);
        Assert.Equal("payload", binding.Name);
        Assert.Equal(bytes, binding.Bytes);
        Assert.Null(binding.Json);
        if (trigger == "rabbitMetadata")
        {
            Assert.Equal("\"binary.route\"", host.BindingData.TriggerMetadataJson!["RoutingKey"]);
            Assert.Equal("\"binary-id\"", host.BindingData.TriggerMetadataJson["MessageId"]);
        }
        else
        {
            Assert.Null(host.BindingData.TriggerMetadataJson);
        }
    }
#pragma warning restore xUnit1051

    [Theory]
    [InlineData("binding", false)]
    [InlineData("binding", true)]
    [InlineData("invocation", false)]
    [InlineData("invocation", true)]
    [InlineData("topic", false)]
    [InlineData("topic", true)]
    [InlineData("rabbit", true)]
    public async Task RawOverloads_NullPayload_Throws(string trigger, bool binaryData)
    {
        using var host = new CapturingHost();
        var token = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<ArgumentNullException>(() => (trigger, binaryData) switch
        {
            ("binding", false) => host.InvokeDaprBindingAsync("Function", (byte[])null!, token),
            ("binding", true) => host.InvokeDaprBindingAsync("Function", (BinaryData)null!, token),
            ("invocation", false) => host.InvokeDaprServiceInvocationAsync("Function", (byte[])null!, token),
            ("invocation", true) => host.InvokeDaprServiceInvocationAsync("Function", (BinaryData)null!, token),
            ("topic", false) => host.InvokeDaprTopicAsync("Function", (byte[])null!, token),
            ("topic", true) => host.InvokeDaprTopicAsync("Function", (BinaryData)null!, token),
            _ => host.InvokeRabbitMQAsync("Function", (BinaryData)null!, token)
        });
        Assert.Null(host.BindingData);
    }

    [Theory]
    [InlineData("binding")]
    [InlineData("invocation")]
    [InlineData("topic")]
    [InlineData("rabbit")]
    public async Task BinaryDataOverloads_InvalidArguments_ThrowBeforeInvocation(string trigger)
    {
        using var host = new CapturingHost();
        var body = new BinaryData(Array.Empty<byte>());
        var token = TestContext.Current.CancellationToken;
        Task<FunctionInvocationResult> Invoke(IFunctionsTestHost target, string name) => trigger switch
        {
            "binding" => target.InvokeDaprBindingAsync(name, body, token),
            "invocation" => target.InvokeDaprServiceInvocationAsync(name, body, token),
            "topic" => target.InvokeDaprTopicAsync(name, body, token),
            _ => target.InvokeRabbitMQAsync(name, body, token)
        };

        await Assert.ThrowsAsync<ArgumentNullException>(() => Invoke(null!, "Function"));
        await Assert.ThrowsAsync<ArgumentException>(() => Invoke(host, ""));
        await Assert.ThrowsAsync<ArgumentNullException>(() => Invoke(host, null!));
        Assert.Null(host.BindingData);
    }

    private sealed class CapturingHost : IFunctionsTestHost, IFunctionInvoker
    {
        public TriggerBindingData? BindingData { get; private set; }
        public IFunctionInvoker Invoker => this;
        public IServiceProvider Services => throw new NotSupportedException();
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task<FunctionInvocationResult> InvokeAsync(
            string functionName,
            FunctionInvocationContext context,
            Func<FunctionInvocationContext, FunctionRegistration, TriggerBindingData> triggerBindingFactory,
            CancellationToken cancellationToken = default)
        {
            BindingData = triggerBindingFactory(context, new("id", functionName, context.TriggerType, "payload"));
            return Task.FromResult(new FunctionInvocationResult { Success = true });
        }

        public IReadOnlyDictionary<string, IFunctionMetadata> GetFunctions() => new Dictionary<string, IFunctionMetadata>();
    }
}
