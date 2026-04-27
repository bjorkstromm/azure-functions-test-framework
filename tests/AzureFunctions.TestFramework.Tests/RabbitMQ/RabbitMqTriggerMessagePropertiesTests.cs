using AzureFunctions.TestFramework.RabbitMQ;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.RabbitMQ;

/// <summary>
/// Unit tests for <see cref="RabbitMqTriggerMessageProperties"/>.
/// </summary>
public class RabbitMqTriggerMessagePropertiesTests
{
    [Fact]
    public void HasAnySet_AllNull_ReturnsFalse()
    {
        var props = new RabbitMqTriggerMessageProperties();
        Assert.False(props.HasAnySet());
    }

    [Fact]
    public void HasAnySet_ExchangeSet_ReturnsTrue()
    {
        var props = new RabbitMqTriggerMessageProperties { Exchange = "my-exchange" };
        Assert.True(props.HasAnySet());
    }

    [Fact]
    public void HasAnySet_RoutingKeySet_ReturnsTrue()
    {
        var props = new RabbitMqTriggerMessageProperties { RoutingKey = "my.route" };
        Assert.True(props.HasAnySet());
    }

    [Fact]
    public void HasAnySet_DeliveryTagSet_ReturnsTrue()
    {
        var props = new RabbitMqTriggerMessageProperties { DeliveryTag = 1UL };
        Assert.True(props.HasAnySet());
    }

    [Fact]
    public void HasAnySet_RedeliveredSet_ReturnsTrue()
    {
        var props = new RabbitMqTriggerMessageProperties { Redelivered = true };
        Assert.True(props.HasAnySet());
    }

    [Fact]
    public void HasAnySet_EmptyHeaders_ReturnsFalse()
    {
        var props = new RabbitMqTriggerMessageProperties
        {
            Headers = new Dictionary<string, string>()
        };
        Assert.False(props.HasAnySet());
    }

    [Fact]
    public void HasAnySet_NonEmptyHeaders_ReturnsTrue()
    {
        var props = new RabbitMqTriggerMessageProperties
        {
            Headers = new Dictionary<string, string> { ["x-custom"] = "val" }
        };
        Assert.True(props.HasAnySet());
    }

    [Fact]
    public void ToTriggerMetadataJson_AllNull_ReturnsNull()
    {
        var props = new RabbitMqTriggerMessageProperties();
        var result = props.ToTriggerMetadataJson();
        Assert.Null(result);
    }

    [Fact]
    public void ToTriggerMetadataJson_Exchange_ContainsEntry()
    {
        var props = new RabbitMqTriggerMessageProperties { Exchange = "amq.direct" };
        var result = props.ToTriggerMetadataJson();
        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("Exchange"));
        Assert.Contains("amq.direct", result["Exchange"]);
    }

    [Fact]
    public void ToTriggerMetadataJson_RoutingKey_ContainsEntry()
    {
        var props = new RabbitMqTriggerMessageProperties { RoutingKey = "orders.created" };
        var result = props.ToTriggerMetadataJson();
        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("RoutingKey"));
        Assert.Contains("orders.created", result["RoutingKey"]);
    }

    [Fact]
    public void ToTriggerMetadataJson_ConsumerTag_ContainsEntry()
    {
        var props = new RabbitMqTriggerMessageProperties { ConsumerTag = "ctag-xyz" };
        var result = props.ToTriggerMetadataJson();
        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("ConsumerTag"));
    }

    [Fact]
    public void ToTriggerMetadataJson_DeliveryTag_ContainsEntry()
    {
        var props = new RabbitMqTriggerMessageProperties { DeliveryTag = 42UL };
        var result = props.ToTriggerMetadataJson();
        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("DeliveryTag"));
        Assert.Contains("42", result["DeliveryTag"]);
    }

    [Fact]
    public void ToTriggerMetadataJson_Redelivered_True_ContainsEntry()
    {
        var props = new RabbitMqTriggerMessageProperties { Redelivered = true };
        var result = props.ToTriggerMetadataJson();
        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("Redelivered"));
        Assert.Contains("true", result["Redelivered"]);
    }

    [Fact]
    public void ToTriggerMetadataJson_Redelivered_False_ContainsEntry()
    {
        var props = new RabbitMqTriggerMessageProperties { Redelivered = false };
        var result = props.ToTriggerMetadataJson();
        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("Redelivered"));
        Assert.Contains("false", result["Redelivered"]);
    }

    [Fact]
    public void ToTriggerMetadataJson_MessageId_ContainsEntry()
    {
        var props = new RabbitMqTriggerMessageProperties { MessageId = "msg-123" };
        var result = props.ToTriggerMetadataJson();
        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("MessageId"));
    }

    [Fact]
    public void ToTriggerMetadataJson_ContentType_ContainsEntry()
    {
        var props = new RabbitMqTriggerMessageProperties { ContentType = "application/json" };
        var result = props.ToTriggerMetadataJson();
        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("ContentType"));
    }

    [Fact]
    public void ToTriggerMetadataJson_CorrelationId_ContainsEntry()
    {
        var props = new RabbitMqTriggerMessageProperties { CorrelationId = "corr-456" };
        var result = props.ToTriggerMetadataJson();
        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("CorrelationId"));
    }

    [Fact]
    public void ToTriggerMetadataJson_ReplyTo_ContainsEntry()
    {
        var props = new RabbitMqTriggerMessageProperties { ReplyTo = "reply-queue" };
        var result = props.ToTriggerMetadataJson();
        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("ReplyTo"));
    }

    [Fact]
    public void ToTriggerMetadataJson_Headers_ContainsEntry()
    {
        var props = new RabbitMqTriggerMessageProperties
        {
            Headers = new Dictionary<string, string> { ["x-trace"] = "abc" }
        };
        var result = props.ToTriggerMetadataJson();
        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("Headers"));
        Assert.Contains("x-trace", result["Headers"]);
    }

    [Fact]
    public void ToTriggerMetadataJson_AllProperties_ContainsAllEntries()
    {
        var props = new RabbitMqTriggerMessageProperties
        {
            Exchange = "ex",
            RoutingKey = "rk",
            ConsumerTag = "ct",
            DeliveryTag = 1UL,
            Redelivered = false,
            MessageId = "mid",
            ContentType = "text/plain",
            CorrelationId = "cid",
            ReplyTo = "rq",
            Headers = new Dictionary<string, string> { ["h"] = "v" }
        };
        var result = props.ToTriggerMetadataJson();
        Assert.NotNull(result);
        Assert.Equal(10, result!.Count);
    }

    [Fact]
    public void ToTriggerMetadataJson_KeysAreCaseInsensitive()
    {
        var props = new RabbitMqTriggerMessageProperties { Exchange = "ex" };
        var result = props.ToTriggerMetadataJson();
        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("exchange"));
        Assert.True(result.ContainsKey("EXCHANGE"));
    }
}
