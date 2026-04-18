# AzureFunctions.TestFramework.Redis

Redis Trigger, Input, and Output binding support for the [Azure Functions Test Framework](../../README.md).

## Installation

```bash
dotnet add package AzureFunctions.TestFramework.Redis
```

## Supported bindings

| Binding | Attribute | Description |
|---------|-----------|-------------|
| `[RedisPubSubTrigger]` | Trigger | Receives a message from a Redis pub/sub channel |
| `[RedisListTrigger]` | Trigger | Receives a value popped from a Redis list |
| `[RedisStreamTrigger]` | Trigger | Receives entries from a Redis stream |
| `[RedisInput]` | Input | Executes a Redis command and injects the result |
| `[RedisOutput]` | Output | Executes a Redis command with the function's return value — captured via `FunctionInvocationResult` |

## Redis Pub/Sub Trigger

Use `InvokeRedisPubSubAsync` to simulate a Redis pub/sub channel message trigger.

```csharp
var result = await host.InvokeRedisPubSubAsync(
    "ProcessPubSubMessage",
    channel: "notifications",
    message: "hello from redis");

Assert.True(result.Success);
```

### Function example

```csharp
[Function("ProcessPubSubMessage")]
public void Run(
    [RedisPubSubTrigger("%RedisConnection%", "notifications")] string message)
{
    _logger.LogInformation("Received pub/sub message: {Message}", message);
}
```

## Redis List Trigger

Use `InvokeRedisListAsync` to simulate a value being popped from a Redis list.

```csharp
var result = await host.InvokeRedisListAsync(
    "ProcessListEntry",
    key: "work-queue",
    value: "task-payload");

Assert.True(result.Success);
```

### Function example

```csharp
[Function("ProcessListEntry")]
public void Run(
    [RedisListTrigger("%RedisConnection%", "work-queue")] string entry)
{
    _logger.LogInformation("Processing list entry: {Entry}", entry);
}
```

## Redis Stream Trigger

Use `InvokeRedisStreamAsync` to simulate a Redis stream entry trigger. The entries are passed as
a list of name-value pairs and serialized to a JSON array of `{"name":"…","value":"…"}` objects.

```csharp
var entries = new[]
{
    new KeyValuePair<string, string>("field1", "value1"),
    new KeyValuePair<string, string>("field2", "value2")
};

var result = await host.InvokeRedisStreamAsync(
    "ProcessStreamEntry",
    key: "mystream",
    entries: entries);

Assert.True(result.Success);
```

### Function example

```csharp
[Function("ProcessStreamEntry")]
public void Run(
    [RedisStreamTrigger("%RedisConnection%", "mystream")] string entriesJson)
{
    _logger.LogInformation("Received stream entries: {Entries}", entriesJson);
}
```

## Redis Input Binding

Register a fake Redis command result via the builder so it is injected automatically for every
invocation of functions that declare a `[RedisInput]` parameter.

```csharp
var host = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunction).Assembly)
    .WithHostBuilderFactory(Program.CreateHostBuilder)
    // Inject the result of "GET mykey" as "cached-value"
    .WithRedisInput("GET mykey", "cached-value")
    .BuildAndStartAsync();
```

Use `WithRedisInputJson(command, json)` to inject a pre-serialized JSON value.

The `command` must exactly match the value declared in the `[RedisInput]` attribute's `command`
argument (case-insensitive).

### Function example

```csharp
[Function("ReadRedisInput")]
public void Run(
    [QueueTrigger("redis-input-queue")] string unused,
    [RedisInput("%RedisConnection%", "GET mykey")] string cachedValue)
{
    _logger.LogInformation("Cached value: {Value}", cachedValue);
}
```

## Redis Output Binding

Output bindings are captured automatically via `FunctionInvocationResult`:

```csharp
var result = await host.InvokeRedisPubSubAsync("WriteRedisOutput", "chan", "my-message");

Assert.True(result.Success);
var written = result.ReadReturnValueAs<string>();
Assert.Equal("my-message", written);
```

### Function example

```csharp
[Function("WriteRedisOutput")]
[RedisOutput("%RedisConnection%", "SET outkey")]
public string? Run(
    [RedisPubSubTrigger("%RedisConnection%", "chan")] string message)
{
    return message;
}
```

## Testing across all four flavours

Add the Redis package reference to your test project and all four function-app test flavours:

```xml
<PackageReference Include="AzureFunctions.TestFramework.Redis" />
```

See the [4-flavour matrix test pattern](../../tests/) for the concrete test class structure.
