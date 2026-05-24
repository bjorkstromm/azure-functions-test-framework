using System.Text;
using AzureFunctions.TestFramework.RabbitMQ;

namespace TestProject;

/// <summary>Tests for RabbitMQ-triggered functions.</summary>
public abstract class RabbitMqTestsBase(ITestOutputHelper output) : TestHostTestBase(output)
{
    private InMemoryProcessedItemsService? _processedItems;

    protected override async Task<IFunctionsTestHost> CreateTestHostAsync()
    {
        _processedItems = new InMemoryProcessedItemsService();
        return await CreateTestHostWithProcessedItemsAsync(_processedItems);
    }

    protected abstract Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(InMemoryProcessedItemsService processedItems);

    [Fact]
    public async Task InvokeRabbitMQAsync_WithStringBody_Succeeds()
    {
        var body = "Hello from RabbitMQ test!";
        var result = await TestHost.InvokeRabbitMQAsync("ProcessRabbitMqMessage", body, TestCancellation);
        Assert.True(result.Success, $"RabbitMQ invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(body, processed[0]);
    }

    [Fact]
    public async Task InvokeRabbitMQAsync_WithByteBody_Succeeds()
    {
        var body = "Hello as UTF-8 bytes!";
        var bytes = Encoding.UTF8.GetBytes(body);
        var result = await TestHost.InvokeRabbitMQAsync("ProcessRabbitMqMessage", bytes, TestCancellation);
        Assert.True(result.Success, $"RabbitMQ byte[] invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(body, processed[0]);
    }

    [Fact]
    public async Task InvokeRabbitMQAsync_WithPocoPayload_Succeeds()
    {
        var payload = new RabbitMQTriggerFunction.RabbitMqOrderPayload { OrderId = "order-99" };
        var result = await TestHost.InvokeRabbitMQAsync("ProcessRabbitMqOrder", payload, cancellationToken: TestCancellation);
        Assert.True(result.Success, $"RabbitMQ POCO invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal("order-99", processed[0]);
    }

    [Fact]
    public async Task InvokeRabbitMQAsync_WithOptionalMessageProperties_PopulatesBindingData()
    {
        var body = "meta-body";
        var props = new RabbitMqTriggerMessageProperties
        {
            RoutingKey = "custom-routing-key",
            MessageId = "msg-id-42",
            Exchange = "test-exchange"
        };

        var result = await TestHost.InvokeRabbitMQAsync("ProcessRabbitMqWithMetadata", body, props, TestCancellation);

        Assert.True(result.Success, $"RabbitMQ metadata invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Contains("custom-routing-key", processed[0], StringComparison.Ordinal);
        Assert.Contains("msg-id-42", processed[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeRabbitMQAsync_WithOutputBinding_CapturesOutputDataAndReadOutputAs()
    {
        var body = "rmq-output-body";
        var result = await TestHost.InvokeRabbitMQAsync("ReturnRabbitMqWithOutput", body, TestCancellation);
        Assert.True(result.Success, $"RabbitMQ invocation failed: {result.Error}");

        Assert.NotEmpty(result.OutputData);
        Assert.Contains(
            "OutboundMessage",
            result.OutputData.Keys,
            StringComparer.OrdinalIgnoreCase);

        var outbound = result.ReadOutputAs<string>("OutboundMessage");
        Assert.Equal($"rabbit-out:{body}", outbound);
    }
}
