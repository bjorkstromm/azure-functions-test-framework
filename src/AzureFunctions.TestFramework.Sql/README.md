# AzureFunctions.TestFramework.Sql

Azure SQL Trigger, Input, and Output binding support for the [Azure Functions Test Framework](../../README.md).

## Installation

```bash
dotnet add package AzureFunctions.TestFramework.Sql
```

## Supported bindings

| Binding | Attribute | Description |
|---------|-----------|-------------|
| `[SqlTrigger]` | Trigger | Receives a batch of row changes from SQL change tracking |
| `[SqlInput]` | Input | Reads rows from a SQL table or view via a query |
| `[SqlOutput]` | Output | Upserts rows into a SQL table — captured via `FunctionInvocationResult` |

## SQL Trigger

Use `InvokeSqlAsync` to simulate a SQL change-tracking trigger with a batch of row changes.

### Strongly-typed changes

```csharp
using Microsoft.Azure.Functions.Worker.Extensions.Sql;

var changes = new[]
{
    new SqlChange<Product>(SqlChangeOperation.Insert, new Product { Id = 1, Name = "Widget" }),
    new SqlChange<Product>(SqlChangeOperation.Update, new Product { Id = 2, Name = "Gadget" })
};

var result = await host.InvokeSqlAsync("ProcessSqlChanges", changes);

Assert.True(result.Success);
```

### Raw JSON

```csharp
var json = """[{"operation":0,"item":{"id":1,"name":"Widget"}}]""";

var result = await host.InvokeSqlAsync("ProcessSqlChanges", json);

Assert.True(result.Success);
```

## SQL Input Binding

Register fake rows via the builder so they are injected automatically for every invocation:

```csharp
var host = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunction).Assembly)
    .WithHostBuilderFactory(Program.CreateHostBuilder)
    // Inject rows for [SqlInput(commandText: "SELECT * FROM Products", commandType: CommandType.Text, ...)]
    .WithSqlInputRows("SELECT * FROM Products",
        new List<Product>
        {
            new Product { Id = 1, Name = "Widget" },
            new Product { Id = 2, Name = "Gadget" }
        })
    .BuildAndStartAsync();
```

Use `WithSqlInputRows<T>(commandText, T row)` to inject a single row,
`WithSqlInputRows<T>(commandText, IReadOnlyList<T> rows)` to inject a list,
and `WithSqlInputJson(commandText, json)` to inject raw JSON.

The `commandText` must exactly match the value declared in the `[SqlInput]` attribute.

### Function example

```csharp
[Function("ReadSqlProducts")]
public void Run(
    [QueueTrigger("products-queue")] string queueMessage,
    [SqlInput(commandText: "SELECT * FROM Products",
              commandType: System.Data.CommandType.Text,
              connectionStringSetting: "SqlConnection")] IEnumerable<Product> products)
{
    foreach (var product in products)
        _logger.LogInformation("Product: {Name}", product.Name);
}
```

## SQL Output Binding

Output bindings are captured automatically via `FunctionInvocationResult`:

```csharp
var changes = new[] { new SqlChange<Product>(SqlChangeOperation.Insert, new Product { Id = 1, Name = "Widget" }) };
var result = await host.InvokeSqlAsync("UpsertSqlProduct", changes);

Assert.True(result.Success);
var written = result.ReadReturnValueAs<Product>();
Assert.Equal(1, written?.Id);
```

### Function example

```csharp
[Function("UpsertSqlProduct")]
[SqlOutput(tableName: "Products", connectionStringSetting: "SqlConnection")]
public Product? Run(
    [SqlTrigger(tableName: "Changes", connectionStringSetting: "SqlConnection")]
    IReadOnlyList<SqlChange<Product>> changes)
{
    return changes.FirstOrDefault()?.Item;
}
```

## Testing across all four flavours

Add the SQL package reference to your test project and all four function-app test flavours:

```xml
<PackageReference Include="AzureFunctions.TestFramework.Sql" />
```

See the [4-flavour matrix test pattern](../../tests/) for the concrete test class structure.
