# AzureFunctions.TestFramework.Tables

[![NuGet](https://img.shields.io/nuget/v/AzureFunctions.TestFramework.Tables.svg)](https://www.nuget.org/packages/AzureFunctions.TestFramework.Tables)

Table input binding support for the Azure Functions Test Framework. Provides `WithTableEntity(...)` and `WithTableEntities(...)` — builder extensions that inject fake Azure Table Storage data for functions with `[TableInput]` parameters in integration tests. Output binding values (`[TableOutput]`) are captured generically by Core's `FunctionInvocationResult` without any per-extension work.

## `[TableInput]` injection

```csharp
// Function under test
[Function("LookupOrder")]
public static string Run(
    [QueueTrigger("lookup-queue")] string rowKey,
    [TableInput("Orders", "customer-1", "{queueTrigger}")] OrderEntity order)
{
    return $"Order: {order.Status}";
}
```

```csharp
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Tables;

// Single entity — matches [TableInput("Orders", "customer-1", "order-42")]
_testHost = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunction).Assembly)
    .WithTableEntity("Orders", "customer-1", "order-42",
        new OrderEntity { PartitionKey = "customer-1", RowKey = "order-42", Status = "Pending" })
    .BuildAndStartAsync();

// Collection — matches [TableInput("Orders")] (no partitionKey / rowKey)
_testHost = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunction).Assembly)
    .WithTableEntities("Orders",
        new[]
        {
            new OrderEntity { PartitionKey = "customer-1", RowKey = "order-1", Status = "Pending" },
            new OrderEntity { PartitionKey = "customer-1", RowKey = "order-2", Status = "Shipped" },
        })
    .BuildAndStartAsync();

// Partition-scoped collection — matches [TableInput("Orders", "customer-1")]
_testHost = await new FunctionsTestHostBuilder()
    .WithFunctionsAssembly(typeof(MyFunction).Assembly)
    .WithTableEntities("Orders", "customer-1",
        new[]
        {
            new OrderEntity { PartitionKey = "customer-1", RowKey = "order-1", Status = "Pending" },
        })
    .BuildAndStartAsync();
```

Lookup is performed from most-specific to least-specific key:
1. `tableName/partitionKey/rowKey` — single entity (matches `[TableInput("T", "pk", "rk")]`)
2. `tableName/partitionKey` — partition-scoped collection (matches `[TableInput("T", "pk")]`)
3. `tableName` — full-table collection (matches `[TableInput("T")]`)

> **Supported parameter types for `[TableInput]`:** POCO types, `TableEntity` / `ITableEntity`, `IEnumerable<T>`. `TableClient` uses a different model-binding-data mechanism and is not supported by `WithTableEntity` / `WithTableEntities`. For `TableClient` parameters, override the Azure Tables SDK client in DI via `ConfigureServices`.

## `[TableOutput]` capture

Output binding values are captured generically by Core's `FunctionInvocationResult.OutputData` without any extra configuration:

```csharp
var result = await _testHost.InvokeQueueAsync("WriteOrder", message);
var entity = result.ReadOutputAs<OrderEntity>("Entity");  // binding name from [TableOutput]
Assert.Equal("customer-1", entity.PartitionKey);
```

## References

- [Full documentation](https://github.com/bjorkstromm/azure-functions-test-framework)
- [`AzureFunctions.TestFramework.Core`](https://www.nuget.org/packages/AzureFunctions.TestFramework.Core)

## License

MIT
