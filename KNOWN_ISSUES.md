# Known Issues

For current capabilities, package layout, and common commands, see `README.md`. For architectural rationale (ALC isolation, FrameworkReference, GrpcWorker no-op, durable converter interception), see the "Architecture & Design Decisions" section in `README.md`. This file is limited to active blockers and known caveats.

Reviewed: 2026-05-24 (README roadmap/"Next likely areas" refreshed; caveats below remain current).

## Active blockers

- No active blockers for the current Worker SDK 2.x sample/test suites (including the latest Durable fake API support and CI-aligned test expectations).
- No active coverage blockers for framework libraries: CI coverage now reports 80%+ line coverage across all `AzureFunctions.TestFramework.*` libraries.
- Warmup trigger support is now available via `AzureFunctions.TestFramework.Warmup` (`InvokeWarmupAsync(...)`) and is covered in the 4-flavour test matrix.

## Known issues

- `FunctionsTestHostBuilder.ConfigureEnvironmentVariable(name, value)` sets process-level environment variables, so tests that need different values for the same variable name should not run in parallel.
- **`[FromBody]` only works in ASP.NET Core integration mode.** In direct gRPC mode, the Worker SDK's `DefaultFromBodyConversionFeature` reads headers from `RpcHttp.NullableHeaders`, which the framework's proto definition does not yet include. Workaround: use `req.ReadFromJsonAsync<T>()` instead of `[FromBody]` in functions tested via direct gRPC mode.
- The durable support package currently uses a framework-owned fake path (`ConfigureFakeDurableSupport(...)` + `FunctionsDurableClientProvider`) instead of the real Durable runtime and execution engine.
- Fake durable pageable query APIs (`GetAllInstancesAsync`, `GetAllEntitiesAsync`) currently return a single in-memory page and ignore continuation tokens; they are intended for test scenarios, not backend-scale pagination fidelity.
- When using `FunctionsApplicationBuilder` with `ConfigureFunctionsWebApplication()`, call `ConfigureFunctionsWebApplication()` **before** any `UseMiddleware<T>()` calls. The SDK's `FunctionsHttpProxyingMiddleware` must run first to populate `FunctionContext.Items["HttpRequestContext"]`; otherwise user middleware calling `GetHttpRequestDataAsync()` poisons the SDK's internal binding cache. The framework includes a cache-clearing middleware as a safety net, but correct ordering ensures the user middleware sees the `HttpContext` it expects.
- **Worker-side logging is suppressed by default.** The worker host's minimum log level is set to `Warning` to keep output clean. If your function code uses `ILogger` and you want to see those logs in test results, use `ConfigureWorkerLogging(logging => { logging.SetMinimumLevel(LogLevel.Information); logging.AddProvider(yourProvider); })`. See the "Worker-side Logging" section in `README.md`.
- **`DurableClientBindingDefaults` moved to the Durable package.** If you previously referenced `AzureFunctions.TestFramework.Core.DurableClientBindingDefaults`, update the `using` directive to `AzureFunctions.TestFramework.Durable.DurableClientBindingDefaults`. This type was only intended for Durable-specific scenarios.
- **`[BlobInput]` / `[BlobTrigger]` with SDK client types (`BlobClient`, `BlockBlobClient`, etc.)** require `WithBlobServiceClient(blobServiceClient)` on the builder. For `[BlobInput]` paths targeting client types, also call `WithBlobInputClient("container/blob")`. The `WithBlobInputContent` extension is for content types only (`string`, `byte[]`, `Stream`, `BinaryData`).
- **`[TableInput]` with `TableClient` parameters is not supported by `WithTableEntity` / `WithTableEntities`.** The `WithTableEntity` / `WithTableEntities` builder extensions inject JSON for the binding, which covers POCO types and `TableEntity` / `ITableEntity` (single entity and collections). `TableClient` uses a different model-binding-data mechanism. For `TableClient` parameters, override the Azure Tables SDK client in DI via `ConfigureServices(services => services.AddSingleton<TableServiceClient>(fakeTableServiceClient))`.
- **`[CosmosDBInput]` with complex SDK types (e.g. `CosmosClient`, `Container`, `DatabaseProxy`) is not supported by `WithCosmosDBInputDocuments`.** The `WithCosmosDBInputDocuments` builder extensions inject JSON for the binding, which covers POCO types and `string`. Complex SDK client types use model-binding-data. For those parameters, override the Cosmos SDK client in DI via `ConfigureServices(services => services.AddSingleton<CosmosClient>(fakeCosmosClient))`.
- **`[SqlInput]` with complex SDK types is not supported by `WithSqlInputRows`.** The `WithSqlInputRows` builder extensions inject JSON for the binding, which covers POCO types and `IEnumerable<T>`. The lookup key is the `commandText` value declared in the `[SqlInput]` attribute (case-insensitive). When using `InvokeSqlAsync(string changesJson)` directly, `SqlChangeOperation` enum values must be integers (0=Insert, 1=Update, 2=Delete) because `System.Text.Json` uses numeric enum serialization by default.
- **`[KustoInput]` lookup is based on `database` + table name parsed from `KqlCommand`.** `WithKustoInputRows(database, table, ...)` matches the first table identifier in the `KqlCommand` (for example `"InputTable | take 10"`). Queries that begin with declarations or non-table expressions may not match this lookup strategy.
- **`[RedisInput]` supports string-typed parameters only via `WithRedisInput`.** The `WithRedisInput(command, value)` builder extension injects a string value keyed by the full `command` string declared in the `[RedisInput]` attribute (case-insensitive, e.g. `"GET mykey"`). For JSON-typed injection use `WithRedisInputJson(command, json)`. Redis trigger methods (`InvokeRedisPubSubAsync`, `InvokeRedisListAsync`, `InvokeRedisStreamAsync`) pass trigger values as `string` binding data; functions whose parameters are typed as `string` receive the raw value directly.
- **`SignalRMessageAction` / `SignalRGroupAction` cannot be deserialized directly via `ReadReturnValueAs<T>()`.** Both types have multiple parameterized constructors with no `[JsonConstructor]` attribute, so `System.Text.Json` cannot select one unambiguously. Read the `[SignalROutput]` return value as `JsonElement` and inspect properties via `GetProperty(...)` instead.

## Design constraints

### One function-app project per test project

The framework enforces a **one function-app project per test project** constraint. When multiple function-app projects compile to the same output directory, the last-built `host.json` overwrites the others. The Azure Functions SDK reads `host.json` from `FUNCTIONS_APPLICATION_DIRECTORY` at runtime, so the "winning" file determines settings like `extensions.http.routePrefix` for **all** test hosts sharing that output directory. Keep a 1:1 mapping between test projects and function-app projects to avoid this.

### Adding a new trigger type

To add support for an additional trigger type without modifying Core:

1. In your `InvokeXxxAsync` static extension method, add a `private static TriggerBindingData CreateBindingData(FunctionInvocationContext context, FunctionRegistration function)` method that builds the gRPC binding data from the context's `InputData`.
2. Pass the factory as the `triggerBindingFactory` argument to `host.Invoker.InvokeAsync(functionName, context, CreateBindingData, cancellationToken)`.
3. If the trigger requires non-trigger input bindings injected synthetically (like `durableClient`), implement `ISyntheticBindingProvider` and register it via `builder.WithSyntheticBindingProvider(...)` in the builder-level extension method.
