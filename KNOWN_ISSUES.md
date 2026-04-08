# Known Issues

For current capabilities, package layout, and common commands, see `README.md`. For architectural rationale (ALC isolation, FrameworkReference, GrpcWorker no-op, durable converter interception), see the "Architecture & Design Decisions" section in `README.md`. This file is limited to active blockers and known caveats.

## Active blockers

- No active blockers for the current Worker SDK 2.x sample/test suites.

## Known issues

- `FunctionsTestHostBuilder.ConfigureEnvironmentVariable(name, value)` sets process-level environment variables, so tests that need different values for the same variable name should not run in parallel.
- The durable support package currently uses a framework-owned fake path (`ConfigureFakeDurableSupport(...)` + `FunctionsDurableClientProvider`) instead of the real Durable runtime and execution engine.
- `AzureFunctions.TestFramework.Http` is still a minimal package with little public surface area today; it is packed/published so the package set stays stable while HTTP-specific helpers are added later.
- When using `FunctionsApplicationBuilder` with `ConfigureFunctionsWebApplication()`, call `ConfigureFunctionsWebApplication()` **before** any `UseMiddleware<T>()` calls. The SDK's `FunctionsHttpProxyingMiddleware` must run first to populate `FunctionContext.Items["HttpRequestContext"]`; otherwise user middleware calling `GetHttpRequestDataAsync()` poisons the SDK's internal binding cache. The framework includes a cache-clearing middleware as a safety net, but correct ordering ensures the user middleware sees the `HttpContext` it expects.
- **Worker-side logging is suppressed by default.** The worker host's minimum log level is set to `Warning` to keep output clean. If your function code uses `ILogger` and you want to see those logs in test results, use `ConfigureWorkerLogging(logging => { logging.SetMinimumLevel(LogLevel.Information); logging.AddProvider(yourProvider); })`. See the "Worker-side Logging" section in `README.md`.
- **`DurableClientBindingDefaults` moved to the Durable package.** If you previously referenced `AzureFunctions.TestFramework.Core.DurableClientBindingDefaults`, update the `using` directive to `AzureFunctions.TestFramework.Durable.DurableClientBindingDefaults`. This type was only intended for Durable-specific scenarios.

## Design constraints

### One function-app project per test project

The framework enforces a **one function-app project per test project** constraint. When multiple function-app projects compile to the same output directory, the last-built `host.json` overwrites the others. The Azure Functions SDK reads `host.json` from `FUNCTIONS_APPLICATION_DIRECTORY` at runtime, so the "winning" file determines settings like `extensions.http.routePrefix` for **all** test hosts sharing that output directory. Keep a 1:1 mapping between test projects and function-app projects to avoid this.

### Adding a new trigger type

To add support for an additional trigger type without modifying Core:

1. In your `InvokeXxxAsync` static extension method, add a `private static TriggerBindingData CreateBindingData(FunctionInvocationContext context, FunctionRegistration function)` method that builds the gRPC binding data from the context's `InputData`.
2. Pass the factory as the `triggerBindingFactory` argument to `host.Invoker.InvokeAsync(functionName, context, CreateBindingData, cancellationToken)`.
3. If the trigger requires non-trigger input bindings injected synthetically (like `durableClient`), implement `ISyntheticBindingProvider` and register it via `builder.WithSyntheticBindingProvider(...)` in the builder-level extension method.
