using System.Text;
using System.Text.Json;
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

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task InvokeRabbitMQAsync_RawPayload_PreservesBytes(bool binaryData, bool empty)
    {
        byte[] bytes = empty ? [] : [0, 255, 128, 195, 40, 0, 42];
        var result = binaryData
            ? await TestHost.InvokeRabbitMQAsync("ProcessRabbitMqBinary", new BinaryData(bytes), cancellationToken: TestCancellation)
            : await TestHost.InvokeRabbitMQAsync("ProcessRabbitMqBinary", bytes, cancellationToken: TestCancellation);

        Assert.True(result.Success, $"RabbitMQ binary invocation failed: {result.Error}");
        Assert.Equal($"{Convert.ToBase64String(bytes)}|rk=|mid=", Assert.Single(_processedItems!.TakeAll()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvokeRabbitMQAsync_RawPayload_PreservesMetadata(bool binaryData)
    {
        byte[] bytes = [0, 255, 128, 195, 40];
        var props = new RabbitMqTriggerMessageProperties { RoutingKey = "binary.route", MessageId = "binary-id" };
        var result = binaryData
            ? await TestHost.InvokeRabbitMQAsync("ProcessRabbitMqBinary", new BinaryData(bytes), props, cancellationToken: TestCancellation)
            : await TestHost.InvokeRabbitMQAsync("ProcessRabbitMqBinary", bytes, props, cancellationToken: TestCancellation);

        Assert.True(result.Success, $"RabbitMQ binary metadata invocation failed: {result.Error}");
        Assert.Equal($"{Convert.ToBase64String(bytes)}|rk=binary.route|mid=binary-id", Assert.Single(_processedItems!.TakeAll()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvokeRabbitMQAsync_Poco_UsesSelectedJsonOptionsAndMetadata(bool customOptions)
    {
        var props = new RabbitMqTriggerMessageProperties { RoutingKey = "json.route", MessageId = "json-id" };
        var options = customOptions ? new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower } : null;
        var result = await TestHost.InvokeRabbitMQAsync("ProcessRabbitMqWithMetadata", new { OrderId = "order-42" },
            props, options, TestCancellation);

        Assert.True(result.Success, $"RabbitMQ JSON invocation failed: {result.Error}");
        var json = customOptions ? """{"order_id":"order-42"}""" : """{"orderId":"order-42"}""";
        Assert.Equal($"{json}|rk=json.route|mid=json-id", Assert.Single(_processedItems!.TakeAll()));
    }

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
