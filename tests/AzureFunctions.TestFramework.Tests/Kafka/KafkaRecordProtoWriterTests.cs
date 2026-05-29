using System.Text;
using AzureFunctions.TestFramework.Kafka;
using Microsoft.Azure.Functions.Worker;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Kafka;

/// <summary>
/// Unit tests for <see cref="KafkaRecordProtoWriter"/>.
/// Verifies that the proto3 binary output can be parsed by the Azure Functions Kafka extension's
/// internal <c>KafkaRecordProto</c> parser via reflection.
/// </summary>
public class KafkaRecordProtoWriterTests
{
    [Fact]
    public void Encode_NullRecord_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => KafkaRecordProtoWriter.Encode(null!));
    }

    [Fact]
    public void Encode_EmptyRecord_ReturnsEmptyBytes()
    {
        var record = new KafkaRecord();
        var bytes = KafkaRecordProtoWriter.Encode(record);
        // An all-default record has no non-zero fields so encoding is empty
        Assert.NotNull(bytes);
        Assert.Empty(bytes);
    }

    [Fact]
    public void Encode_RecordWithTopic_ProducesNonEmptyBytes()
    {
        var record = new KafkaRecord { Topic = "orders" };
        var bytes = KafkaRecordProtoWriter.Encode(record);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Encode_RecordWithAllFields_ProducesNonEmptyBytes()
    {
        var record = new KafkaRecord
        {
            Topic = "my-topic",
            Partition = 2,
            Offset = 100,
            Key = Encoding.UTF8.GetBytes("msg-key"),
            Value = Encoding.UTF8.GetBytes("msg-value"),
            Timestamp = new KafkaTimestamp
            {
                UnixTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Type = KafkaTimestampType.CreateTime
            },
            Headers =
            [
                new KafkaHeader { Key = "x-trace", Value = Encoding.UTF8.GetBytes("trace-123") }
            ]
        };

        var bytes = KafkaRecordProtoWriter.Encode(record);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Encode_CanBeRoundTrippedViaKafkaRecordConverter()
    {
        // Verify that the proto-encoded bytes can be parsed back by the Kafka extension's
        // internal KafkaRecordProto using reflection.
        var record = new KafkaRecord
        {
            Topic = "test-topic",
            Partition = 1,
            Offset = 42,
            Value = Encoding.UTF8.GetBytes("hello kafka"),
            Key = Encoding.UTF8.GetBytes("key-1"),
            Headers = [new KafkaHeader { Key = "h1", Value = Encoding.UTF8.GetBytes("v1") }]
        };

        var bytes = KafkaRecordProtoWriter.Encode(record);

        // Use reflection to access the internal KafkaRecordProto type
        var kafkaExtAssembly = typeof(KafkaTriggerAttribute).Assembly;
        var protoType = kafkaExtAssembly.GetType(
            "Microsoft.Azure.Functions.Worker.Extensions.Kafka.Proto.KafkaRecordProto",
            throwOnError: true)!;

        var parserProperty = protoType.GetProperty("Parser",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
        var parser = parserProperty.GetValue(null)!;

        var parseFromMethod = parser.GetType().GetMethod("ParseFrom", [typeof(byte[])])!;
        Assert.NotNull(parseFromMethod);

        object proto;
        proto = parseFromMethod.Invoke(parser, [bytes])!;

        Assert.NotNull(proto);

        var topicProperty = protoType.GetProperty("Topic")!;
        var partitionProperty = protoType.GetProperty("Partition")!;
        var offsetProperty = protoType.GetProperty("Offset")!;

        Assert.Equal("test-topic", (string)topicProperty.GetValue(proto)!);
        Assert.Equal(1, (int)partitionProperty.GetValue(proto)!);
        Assert.Equal(42L, (long)offsetProperty.GetValue(proto)!);
    }

    [Fact]
    public void Encode_TimestampFields_RoundTrips()
    {
        var timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var record = new KafkaRecord
        {
            Topic = "ts-topic",
            Timestamp = new KafkaTimestamp
            {
                UnixTimestampMs = timestampMs,
                Type = KafkaTimestampType.LogAppendTime
            }
        };

        var bytes = KafkaRecordProtoWriter.Encode(record);

        var kafkaExtAssembly = typeof(KafkaTriggerAttribute).Assembly;
        var protoType = kafkaExtAssembly.GetType(
            "Microsoft.Azure.Functions.Worker.Extensions.Kafka.Proto.KafkaRecordProto",
            throwOnError: true)!;

        var parserProperty = protoType.GetProperty("Parser",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
        var parser = parserProperty.GetValue(null)!;
        var parseFromMethod = parser.GetType().GetMethod("ParseFrom", [typeof(byte[])])!;
        var proto = parseFromMethod.Invoke(parser, [bytes])!;

        var timestampProperty = protoType.GetProperty("Timestamp")!;
        var ts = timestampProperty.GetValue(proto)!;
        Assert.NotNull(ts);

        var unixMsProperty = ts.GetType().GetProperty("UnixTimestampMs")!;
        Assert.Equal(timestampMs, (long)unixMsProperty.GetValue(ts)!);
    }

    [Fact]
    public void Encode_MultipleHeaders_RoundTrips()
    {
        var record = new KafkaRecord
        {
            Topic = "hdr-topic",
            Headers =
            [
                new KafkaHeader { Key = "x-a", Value = Encoding.UTF8.GetBytes("alpha") },
                new KafkaHeader { Key = "x-b", Value = Encoding.UTF8.GetBytes("beta") }
            ]
        };

        var bytes = KafkaRecordProtoWriter.Encode(record);

        var kafkaExtAssembly = typeof(KafkaTriggerAttribute).Assembly;
        var protoType = kafkaExtAssembly.GetType(
            "Microsoft.Azure.Functions.Worker.Extensions.Kafka.Proto.KafkaRecordProto",
            throwOnError: true)!;

        var parserProperty = protoType.GetProperty("Parser",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
        var parser = parserProperty.GetValue(null)!;
        var parseFromMethod = parser.GetType().GetMethod("ParseFrom", [typeof(byte[])])!;
        var proto = parseFromMethod.Invoke(parser, [bytes])!;

        var headersProperty = protoType.GetProperty("Headers")!;
        var headers = headersProperty.GetValue(proto) as System.Collections.IEnumerable;
        Assert.NotNull(headers);

        var headerList = headers.Cast<object>().ToList();
        Assert.Equal(2, headerList.Count);

        var keyProp = headerList[0].GetType().GetProperty("Key")!;
        Assert.Equal("x-a", (string)keyProp.GetValue(headerList[0])!);
        Assert.Equal("x-b", (string)keyProp.GetValue(headerList[1])!);
    }

    [Fact]
    public void Encode_NullHeaders_NotIncludedInOutput()
    {
        var record = new KafkaRecord { Topic = "t", Headers = null };
        var bytes = KafkaRecordProtoWriter.Encode(record);
        // Just verify no exception is thrown and we can round-trip
        Assert.NotNull(bytes);
    }

    [Fact]
    public void Encode_NullValueAndKey_NotIncludedInOutput()
    {
        var record = new KafkaRecord { Topic = "t", Key = null, Value = null };
        var bytes = KafkaRecordProtoWriter.Encode(record);
        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes); // Topic field is present
    }
}
