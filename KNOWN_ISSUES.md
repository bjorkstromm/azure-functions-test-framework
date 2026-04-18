# Known Issues

For current capabilities, package layout, and common commands, see `README.md`. For architectural rationale (ALC isolation, FrameworkReference, GrpcWorker no-op, durable converter interception), see the "Architecture & Design Decisions" section in `README.md`. This file is limited to active blockers and known caveats.

## Active blockers

- No active blockers for the current Worker SDK 2.x sample/test suites.

## Known issues

- `FunctionsTestHostBuilder.ConfigureEnvironmentVariable(name, value)` sets process-level environment variables, so tests that need different values for the same variable name should not run in parallel.
- **`[FromBody]` only works in ASP.NET Core integration mode.** In direct gRPC mode, the Worker SDK's `DefaultFromBodyConversionFeature` reads headers from `RpcHttp.NullableHeaders`, which the framework's proto definition does not yet include. Workaround: use `req.ReadFromJsonAsync<T>()` instead of `[FromBody]` in functions tested via direct gRPC mode.
- The durable support package currently uses a framework-owned fake path (`ConfigureFakeDurableSupport(...)` + `FunctionsDurableClientProvider`) instead of the real Durable runtime and execution engine.
- `AzureFunctions.TestFramework.Http` is the HTTP client package (`CreateHttpClient()`, request/response mapping, forwarding handlers for both direct gRPC and ASP.NET Core integration modes). Public surface is intentionally small — it is packed/published as a first-class package.
- When using `FunctionsApplicationBuilder` with `ConfigureFunctionsWebApplication()`, call `ConfigureFunctionsWebApplication()` **before** any `UseMiddleware<T>()` calls. The SDK's `FunctionsHttpProxyingMiddleware` must run first to populate `FunctionContext.Items["HttpRequestContext"]`; otherwise user middleware calling `GetHttpRequestDataAsync()` poisons the SDK's internal binding cache. The framework includes a cache-clearing middleware as a safety net, but correct ordering ensures the user middleware sees the `HttpContext` it expects.
- **Worker-side logging is suppressed by default.** The worker host's minimum log level is set to `Warning` to keep output clean. If your function code uses `ILogger` and you want to see those logs in test results, use `ConfigureWorkerLogging(logging => { logging.SetMinimumLevel(LogLevel.Information); logging.AddProvider(yourProvider); })`. See the "Worker-side Logging" section in `README.md`.
- **`DurableClientBindingDefaults` moved to the Durable package.** If you previously referenced `AzureFunctions.TestFramework.Core.DurableClientBindingDefaults`, update the `using` directive to `AzureFunctions.TestFramework.Durable.DurableClientBindingDefaults`. This type was only intended for Durable-specific scenarios.
- **`[BlobInput]` / `[BlobTrigger]` with SDK client types (`BlobClient`, `BlockBlobClient`, etc.)** require `WithBlobServiceClient(blobServiceClient)` on the builder. For `[BlobInput]` paths targeting client types, also call `WithBlobInputClient("container/blob")`. The `WithBlobInputContent` extension is for content types only (`string`, `byte[]`, `Stream`, `BinaryData`).
- **`[TableInput]` with `TableClient` parameters is not supported by `WithTableEntity` / `WithTableEntities`.** The `WithTableEntity` / `WithTableEntities` builder extensions inject JSON for the binding, which covers POCO types and `TableEntity` / `ITableEntity` (single entity and collections). `TableClient` uses a different model-binding-data mechanism. For `TableClient` parameters, override the Azure Tables SDK client in DI via `ConfigureServices(services => services.AddSingleton<TableServiceClient>(fakeTableServiceClient))`.
- **`[CosmosDBInput]` with complex SDK types (e.g. `CosmosClient`, `Container`, `DatabaseProxy`) is not supported by `WithCosmosDBInputDocuments`.** The `WithCosmosDBInputDocuments` builder extensions inject JSON for the binding, which covers POCO types and `string`. Complex SDK client types use model-binding-data. For those parameters, override the Cosmos SDK client in DI via `ConfigureServices(services => services.AddSingleton<CosmosClient>(fakeCosmosClient))`.
- **`[SqlInput]` with complex SDK types is not supported by `WithSqlInputRows`.** The `WithSqlInputRows` builder extensions inject JSON for the binding, which covers POCO types and `IEnumerable<T>`. The lookup key is the `commandText` value declared in the `[SqlInput]` attribute (case-insensitive). When using `InvokeSqlAsync(string changesJson)` directly, `SqlChangeOperation` enum values must be integers (0=Insert, 1=Update, 2=Delete) because `System.Text.Json` uses numeric enum serialization by default.
- **`SignalRMessageAction` / `SignalRGroupAction` cannot be deserialized directly via `ReadReturnValueAs<T>()`.** Both types have multiple parameterized constructors with no `[JsonConstructor]` attribute, so `System.Text.Json` cannot select one unambiguously. Read the `[SignalROutput]` return value as `JsonElement` and inspect properties via `GetProperty(...)` instead.
- **MCP trigger support requires the `AzureFunctions.TestFramework.Mcp` package.** MCP (Model Context Protocol) tool, resource, and prompt triggers are invoked via `InvokeMcpToolAsync(...)`, `InvokeMcpResourceAsync(...)`, and `InvokeMcpPromptAsync(...)` extension methods. The framework automatically invokes the MCP extension's `WorkerExtensionStartupCodeExecutor` from the functions assembly (working around the SDK's `Assembly.GetEntryAssembly()` limitation in test runners) so that `FunctionsMcpContextMiddleware` runs and populates `FunctionContext.Items` before the function body executes.

## Design constraints

### One function-app project per test project

The framework enforces a **one function-app project per test project** constraint. When multiple function-app projects compile to the same output directory, the last-built `host.json` overwrites the others. The Azure Functions SDK reads `host.json` from `FUNCTIONS_APPLICATION_DIRECTORY` at runtime, so the "winning" file determines settings like `extensions.http.routePrefix` for **all** test hosts sharing that output directory. Keep a 1:1 mapping between test projects and function-app projects to avoid this.

### Adding a new trigger type

To add support for an additional trigger type without modifying Core:

1. In your `InvokeXxxAsync` static extension method, add a `private static TriggerBindingData CreateBindingData(FunctionInvocationContext context, FunctionRegistration function)` method that builds the gRPC binding data from the context's `InputData`.
2. Pass the factory as the `triggerBindingFactory` argument to `host.Invoker.InvokeAsync(functionName, context, CreateBindingData, cancellationToken)`.
3. If the trigger requires non-trigger input bindings injected synthetically (like `durableClient`), implement `ISyntheticBindingProvider` and register it via `builder.WithSyntheticBindingProvider(...)` in the builder-level extension method.
