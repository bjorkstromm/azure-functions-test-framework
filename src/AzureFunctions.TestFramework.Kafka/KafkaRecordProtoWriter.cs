using System.Text;

namespace AzureFunctions.TestFramework.Kafka;

/// <summary>
/// Encodes a <see cref="global::Microsoft.Azure.Functions.Worker.KafkaRecord"/> to the proto3 binary
/// format expected by the Azure Functions Kafka extension's internal <c>KafkaRecordProto</c> parser
/// (<c>AzureKafkaRecord</c> binding source, <c>application/x-protobuf</c> content type).
/// </summary>
/// <remarks>
/// The proto3 schema used here mirrors the internal <c>KafkaRecordProto</c> definition bundled
/// with <c>Microsoft.Azure.Functions.Worker.Extensions.Kafka</c>:
/// <code>
/// message KafkaRecordProto {
///   string topic          = 1;
///   int32  partition      = 2;
///   int64  offset         = 3;
///   bytes  key            = 4;  // optional
///   bytes  value          = 5;  // optional
///   KafkaTimestampProto timestamp = 6;
///   repeated KafkaHeaderProto headers = 7;
///   int32  leader_epoch   = 8;  // optional
/// }
///
/// message KafkaTimestampProto {
///   int64 unix_timestamp_ms = 1;
///   int32 type              = 2;
/// }
///
/// message KafkaHeaderProto {
///   string key   = 1;
///   bytes  value = 2;  // optional
/// }
/// </code>
/// </remarks>
internal static class KafkaRecordProtoWriter
{
    // Proto3 wire types
    private const int WireTypeVarint = 0;
    private const int WireTypeLengthDelimited = 2;

    /// <summary>
    /// Encodes a single <see cref="global::Microsoft.Azure.Functions.Worker.KafkaRecord"/> to proto3 binary bytes.
    /// </summary>
    internal static byte[] Encode(global::Microsoft.Azure.Functions.Worker.KafkaRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        using var ms = new MemoryStream();
        WriteRecord(ms, record);
        return ms.ToArray();
    }

    private static void WriteRecord(Stream stream, global::Microsoft.Azure.Functions.Worker.KafkaRecord record)
    {
        // field 1: topic (string)
        if (!string.IsNullOrEmpty(record.Topic))
        {
            WriteTag(stream, 1, WireTypeLengthDelimited);
            WriteBytes(stream, Encoding.UTF8.GetBytes(record.Topic));
        }

        // field 2: partition (int32)
        if (record.Partition != 0)
        {
            WriteTag(stream, 2, WireTypeVarint);
            WriteVarint(stream, (ulong)record.Partition);
        }

        // field 3: offset (int64)
        if (record.Offset != 0)
        {
            WriteTag(stream, 3, WireTypeVarint);
            WriteVarint(stream, (ulong)record.Offset);
        }

        // field 4: key (bytes, optional)
        if (record.Key is { Length: > 0 })
        {
            WriteTag(stream, 4, WireTypeLengthDelimited);
            WriteBytes(stream, record.Key);
        }

        // field 5: value (bytes, optional)
        if (record.Value is { Length: > 0 })
        {
            WriteTag(stream, 5, WireTypeLengthDelimited);
            WriteBytes(stream, record.Value);
        }

        // field 6: timestamp (embedded message, optional)
        if (record.Timestamp is not null)
        {
            using var ts = new MemoryStream();
            WriteTimestamp(ts, record.Timestamp);
            var tsBytes = ts.ToArray();
            WriteTag(stream, 6, WireTypeLengthDelimited);
            WriteBytes(stream, tsBytes);
        }

        // field 7: headers (repeated)
        if (record.Headers is not null)
        {
            foreach (var header in record.Headers)
            {
                if (header is null)
                    continue;

                using var hdr = new MemoryStream();
                WriteHeader(hdr, header);
                var hdrBytes = hdr.ToArray();
                WriteTag(stream, 7, WireTypeLengthDelimited);
                WriteBytes(stream, hdrBytes);
            }
        }

        // field 8: leader_epoch (int32, optional)
        if (record.LeaderEpoch.HasValue && record.LeaderEpoch.Value != 0)
        {
            WriteTag(stream, 8, WireTypeVarint);
            WriteVarint(stream, (ulong)record.LeaderEpoch.Value);
        }
    }

    private static void WriteTimestamp(
        Stream stream,
        global::Microsoft.Azure.Functions.Worker.KafkaTimestamp timestamp)
    {
        // field 1: unix_timestamp_ms (int64)
        if (timestamp.UnixTimestampMs != 0)
        {
            WriteTag(stream, 1, WireTypeVarint);
            WriteVarint(stream, (ulong)timestamp.UnixTimestampMs);
        }

        // field 2: type (int32/enum)
        int typeValue = (int)timestamp.Type;
        if (typeValue != 0)
        {
            WriteTag(stream, 2, WireTypeVarint);
            WriteVarint(stream, (ulong)typeValue);
        }
    }

    private static void WriteHeader(
        Stream stream,
        global::Microsoft.Azure.Functions.Worker.KafkaHeader header)
    {
        // field 1: key (string)
        if (!string.IsNullOrEmpty(header.Key))
        {
            WriteTag(stream, 1, WireTypeLengthDelimited);
            WriteBytes(stream, Encoding.UTF8.GetBytes(header.Key));
        }

        // field 2: value (bytes, optional)
        if (header.Value is { Length: > 0 })
        {
            WriteTag(stream, 2, WireTypeLengthDelimited);
            WriteBytes(stream, header.Value);
        }
    }

    private static void WriteTag(Stream stream, int fieldNumber, int wireType)
    {
        WriteVarint(stream, (ulong)((fieldNumber << 3) | wireType));
    }

    private static void WriteBytes(Stream stream, byte[] bytes)
    {
        WriteVarint(stream, (ulong)bytes.Length);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteVarint(Stream stream, ulong value)
    {
        while (value > 0x7F)
        {
            stream.WriteByte((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        stream.WriteByte((byte)value);
    }
}
