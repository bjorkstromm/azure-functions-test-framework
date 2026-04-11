# AzureFunctions.TestFramework.CosmosDB

CosmosDB Trigger, Input, and Output binding support for the [Azure Functions Test Framework](../../README.md).

## Installation

```bash
dotnet add package AzureFunctions.TestFramework.CosmosDB
```

## Supported bindings

| Binding | Attribute | Description |
|---------|-----------|-------------|
| `[CosmosDBTrigger]` | Trigger | Receives a batch of changed documents from the CosmosDB change feed |
| `[CosmosDBInput]` | Input | Reads documents from CosmosDB (point-read or query) |
| `[CosmosDBOutput]` | Output | Writes documents to CosmosDB — captured via `FunctionInvocationResult` |

## CosmosDB Trigger

Use `InvokeCosmosDBAsync` to simulate a CosmosDB change-feed trigger with a batch of changed documents.

### Strongly-typed documents

```csharp
var documents = new[]
{
    new TodoItem { Id = "1", Title = "Buy milk", IsComplete = false },
    new TodoItem { Id = "2", Title = "Write tests", IsComplete = true }
};

var result = await host.InvokeCosmosDBAsync("ProcessCosmosDocuments", documents);

Assert.True(result.Success);
```

### Raw JSON

```csharp
var json = """[{"id":"1","title":"Buy milk"},{"id":"2","title":"Write tests"}]""";

var result = await host.InvokeCosmosDBAsync("ProcessCosmosDocuments", json);

Assert.True(result.Success);
```

## CosmosDB Input Binding

Register fake documents via the builder so they are injected automatically for every invocation:

```csharp
var host = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunction).Assembly)
    .WithHostBuilderFactory(Program.CreateHostBuilder)
    // Single document for [CosmosDBInput(databaseName: "MyDb", containerName: "Items")]
    .WithCosmosDBInputDocuments("MyDb", "Items",
        new TodoItem { Id = "1", Title = "Injected item", IsComplete = false })
    .BuildAndStartAsync();
```

Use `WithCosmosDBInputDocuments<T>(databaseName, containerName, IReadOnlyList<T> documents)` to inject a list
of documents, and `WithCosmosDBInputJson(databaseName, containerName, json)` to inject raw JSON.

### Function example

```csharp
[Function("ReadCosmosItem")]
public void Run(
    [QueueTrigger("items-queue")] string queueMessage,
    [CosmosDBInput(databaseName: "MyDb", containerName: "Items", Id = "%itemId%")] TodoItem? item)
{
    if (item is not null)
        _logger.LogInformation("Read item: {Title}", item.Title);
}
```

## CosmosDB Output Binding

Output bindings are captured automatically via `FunctionInvocationResult`:

```csharp
var result = await host.InvokeCosmosDBAsync("CreateCosmosDocument", documents);

Assert.True(result.Success);
var written = result.ReadOutputAs<TodoItem>("outputDocument");
Assert.Equal("expected-id", written?.Id);
```

### Function example

```csharp
[Function("CreateCosmosDocument")]
[CosmosDBOutput(databaseName: "MyDb", containerName: "Items")]
public TodoItem? Run([CosmosDBTrigger(...)] IReadOnlyList<TodoItem> documents)
{
    return documents.FirstOrDefault();
}
```

## Testing across all four flavours

Add the CosmosDB package reference to your test project and all four function-app test flavours:

```xml
<PackageReference Include="AzureFunctions.TestFramework.CosmosDB" />
```

See the [4-flavour matrix test pattern](../../tests/) for the concrete test class structure.
