# Known Issues

For current capabilities, package layout, and common commands, see `README.md`. For architectural rationale (ALC isolation, FrameworkReference, GrpcWorker no-op, durable converter interception), see the "Architecture & Design Decisions" section in `README.md`. This file is limited to active blockers and known caveats.

## Active blockers

- No active blockers for the current Worker SDK 2.x sample/test suites.

## Known issues

- `FunctionsTestHostBuilder.ConfigureEnvironmentVariable(name, value)` sets process-level environment variables, so tests that need different values for the same variable name should not run in parallel.
- The durable support package currently uses a framework-owned fake path (`ConfigureFakeDurableSupport(...)` + `FunctionsDurableClientProvider`) instead of the real Durable runtime and execution engine.
- `AzureFunctions.TestFramework.Http` is still a minimal package with little public surface area today; it is packed/published so the package set stays stable while HTTP-specific helpers are added later.
- When using `FunctionsApplicationBuilder` with `ConfigureFunctionsWebApplication()`, call `ConfigureFunctionsWebApplication()` **before** any `UseMiddleware<T>()` calls. The SDK's `FunctionsHttpProxyingMiddleware` must run first to populate `FunctionContext.Items["HttpRequestContext"]`; otherwise user middleware calling `GetHttpRequestDataAsync()` poisons the SDK's internal binding cache. The framework includes a cache-clearing middleware as a safety net, but correct ordering ensures the user middleware sees the `HttpContext` it expects.

## Design constraints

### One function-app project per test project

The framework enforces a **one function-app project per test project** constraint. When multiple function-app projects compile to the same output directory, the last-built `host.json` overwrites the others. The Azure Functions SDK reads `host.json` from `FUNCTIONS_APPLICATION_DIRECTORY` at runtime, so the "winning" file determines settings like `extensions.http.routePrefix` for **all** test hosts sharing that output directory. Keep a 1:1 mapping between test projects and function-app projects to avoid this.
