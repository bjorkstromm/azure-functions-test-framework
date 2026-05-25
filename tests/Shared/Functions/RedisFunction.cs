using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Redis;
using Microsoft.Extensions.Logging;

namespace TestProject;

/// <summary>
/// Functions that exercise Redis pub/sub trigger, list trigger, stream trigger,
/// input binding, and output binding.
/// </summary>
public class RedisFunction
{
    /// <summary>The Redis connection setting name used by all Redis bindings.</summary>
    public const string ConnectionSetting = "RedisConnection";

    /// <summary>The Redis pub/sub channel name used by trigger tests.</summary>
    public const string PubSubChannel = "test-channel";

    /// <summary>The Redis list key used by list trigger tests.</summary>
    public const string ListKey = "test-list";

    /// <summary>The Redis stream key used by stream trigger tests.</summary>
    public const string StreamKey = "test-stream";

    /// <summary>The Redis command used for input binding tests.</summary>
    public const string InputCommand = "GET test-input-key";

    private readonly ILogger<RedisFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    public RedisFunction(ILogger<RedisFunction> logger, IProcessedItemsService processedItems)
    {
        _logger = logger;
        _processedItems = processedItems;
    }

    /// <summary>
    /// Redis pub/sub trigger function that records the received message.
    /// </summary>
    [Function("ProcessRedisPubSub")]
    public void RunPubSub(
        [RedisPubSubTrigger(ConnectionSetting, PubSubChannel)] string message)
    {
        _logger.LogInformation("Redis pub/sub message received on channel '{Channel}': {Message}",
            PubSubChannel, message);
        _processedItems.Add($"pubsub:{message}");
    }

    /// <summary>
    /// Redis list trigger function that records the received list entry value.
    /// </summary>
    [Function("ProcessRedisList")]
    public void RunList(
        [RedisListTrigger(ConnectionSetting, ListKey)] string entry)
    {
        _logger.LogInformation("Redis list entry received from key '{Key}': {Entry}",
            ListKey, entry);
        _processedItems.Add($"list:{entry}");
    }

    /// <summary>
    /// Redis stream trigger function that records the raw JSON stream entries.
    /// </summary>
    [Function("ProcessRedisStream")]
    public void RunStream(
        [RedisStreamTrigger(ConnectionSetting, StreamKey)] string entries)
    {
        _logger.LogInformation("Redis stream entries received from key '{Key}': {Entries}",
            StreamKey, entries);
        _processedItems.Add($"stream:{entries}");
    }

    /// <summary>
    /// Redis pub/sub trigger function with a Redis output binding.
    /// Echoes the received message as the return value.
    /// </summary>
    [Function("EchoRedisPubSubWithOutput")]
    [RedisOutput(ConnectionSetting, "SET outkey")]
    public string? RunPubSubWithOutput(
        [RedisPubSubTrigger(ConnectionSetting, PubSubChannel)] string message)
    {
        _logger.LogInformation("Echoing pub/sub message: {Message}", message);
        _processedItems.Add(message);
        return message;
    }

    /// <summary>
    /// Queue-triggered function that reads a Redis input binding and records the cached value.
    /// </summary>
    [Function("ReadRedisInput")]
    public void ReadRedisInput(
        [QueueTrigger("redis-input-queue")] string unused,
        [RedisInput(ConnectionSetting, InputCommand)] string cachedValue)
    {
        _logger.LogInformation("Redis input value for command '{Command}': {Value}",
            InputCommand, cachedValue);
        _processedItems.Add(cachedValue);
    }
}
