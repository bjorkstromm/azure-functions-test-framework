# AzureFunctions.TestFramework.DataExplorer

Azure Data Explorer (Kusto) Input and Output binding support for the [Azure Functions Test Framework](https://github.com/bjorkstromm/azure-functions-test-framework).

## Installation

```bash
dotnet add package AzureFunctions.TestFramework.DataExplorer
```

## Supported bindings

| Binding | Attribute | Description |
|---------|-----------|-------------|
| `[KustoInput]` | Input | Reads rows from an Azure Data Explorer query result |
| `[KustoOutput]` | Output | Ingests rows into an Azure Data Explorer table — captured via `FunctionInvocationResult` |

> ℹ️ Azure Data Explorer has no trigger binding in the isolated worker extension. Use another trigger type (for example Queue, Timer, or HTTP) and assert Kusto input/output behavior in that invocation.

## Kusto Input Binding

Register fake rows via the builder so they are injected automatically for every invocation:

```csharp
var host = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunction).Assembly)
    .WithHostBuilderFactory(Program.CreateHostBuilder)
    // Inject rows for [KustoInput("MyDb", KqlCommand = "InputTable | take 10")]
    .WithKustoInputRows(
        database: "MyDb",
        table: "InputTable",
        rows:
        [
            new Product { Id = 1, Name = "Widget" },
            new Product { Id = 2, Name = "Gadget" }
        ])
    .BuildAndStartAsync();
```

Use `WithKustoInputRows<T>(database, table, T row)` to inject a single row,
`WithKustoInputRows<T>(database, table, IReadOnlyList<T> rows)` to inject multiple rows,
and `WithKustoInputJson(database, table, json)` to inject raw JSON.

### Function example

```csharp
[Function("ReadKustoProducts")]
public void Run(
    [QueueTrigger("kusto-input-queue")] string queueMessage,
    [KustoInput("MyDb", KqlCommand = "InputTable | take 10")] IReadOnlyList<Product> products)
{
    foreach (var product in products)
        _logger.LogInformation("Product: {Name}", product.Name);
}
```

## Kusto Output Binding

Output bindings are captured automatically via `FunctionInvocationResult`:

```csharp
var result = await host.InvokeQueueAsync("WriteKustoOutput", "source");

Assert.True(result.Success);
var written = result.ReadReturnValueAs<Product>();
Assert.Equal("copy:source", written?.Name);
```

### Function example

```csharp
[Function("WriteKustoOutput")]
[KustoOutput("MyDb", TableName = "OutputTable")]
public Product Run([QueueTrigger("kusto-output-queue")] string message)
{
    return new Product { Id = 1, Name = $"copy:{message}" };
}
```

## Known limitation

`WithKustoInputRows(database, table, ...)` matches by `database` and the first table identifier in `KqlCommand` (for example `"InputTable | take 10"`). If your query starts with `declare` statements or does not begin with a table identifier, use `WithKustoInputJson(...)` with a matching `database/table` registration strategy in your tests.

## Testing across all four flavours

Add the Data Explorer package reference to your test project and all four function-app test flavours:

```xml
<PackageReference Include="AzureFunctions.TestFramework.DataExplorer" />
```

See the [4-flavour matrix test pattern](https://github.com/bjorkstromm/azure-functions-test-framework/tree/main/tests/) for the concrete test class structure.
