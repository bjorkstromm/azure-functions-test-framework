# Known Issues

For current capabilities, package layout, and common commands, see `README.md`. This file is intentionally limited to active blockers and known issues.

## Active blockers

- No active blockers for the current Worker SDK 2.x sample/test suites.

## Known issues

- `FunctionsTestHostBuilder.ConfigureEnvironmentVariable(name, value)` sets process-level environment variables, so tests that need different values for the same variable name should not run in parallel.
- The durable support package currently uses a framework-owned fake path (`ConfigureFakeDurableSupport(...)` + `FunctionsDurableClientProvider`) instead of the real Durable runtime and execution engine.
- `AzureFunctions.TestFramework.Http` is still a minimal package with little public surface area today; it is packed/published so the package set stays stable while HTTP-specific helpers are added later.

## Resolved issues

- **`FunctionContext` parameter null in `ConfigureFunctionsWebApplication` functions** — When using the framework in a real-world project (functions assembly in a different directory from the test output), the Worker SDK's built-in `FunctionContextConverter` could return `Unhandled()` due to `AssemblyLoadContext` (ALC) isolation. The Worker SDK loads `Microsoft.Azure.Functions.Worker.Extensions.dll` in an isolated ALC (from `.azurefunctions`). That ALC re-loads `Microsoft.Azure.Functions.Worker.Core.dll` from a different physical path than the test process's default ALC, producing two `FunctionContext` type objects with different runtime type handles. The SDK's converter checks `context.TargetType == typeof(FunctionContext)` using the isolated ALC's `FunctionContext` — which doesn't match the default ALC's `FunctionContext` — and returns `Unhandled()`. Fixed by registering a `FunctionContextInputConverter` fallback in `WorkerHostService` that lives in the default ALC; its `typeof(FunctionContext)` matches `context.TargetType`, so it successfully binds `FunctionContext` parameters after the SDK's converter returns `Unhandled()`.

- **Functions without explicit `Route =` not matched** — HTTP trigger functions that omit `Route =` in `[HttpTrigger]` were silently skipped during route registration in `GrpcHostService.HandleFunctionsMetadataResponse`. The code only processed bindings that had a non-null `route` property in the raw binding JSON, causing `SendInvocationRequestAsync` to find no matching function and log "No function found". Fixed by defaulting `route` to `functionMetadata.Name` when the `route` property is absent or null, matching Azure Functions' own built-in default behaviour.

- **`HttpContextConverter` unable to read `HttpRequest` from `FunctionContext`** — When using the test framework in a project where the function app declares `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, `HttpContextConverter.GetHttpContext()` could silently return `null` (the `as HttpContext` cast fails) because two physical copies of `Microsoft.AspNetCore.Http.Abstractions.dll` could be loaded in the same process — one from the shared framework (functions app) and one via the NuGet package chain (test framework). Fixed by adding an explicit `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to `AzureFunctions.TestFramework.Core` and `AzureFunctions.TestFramework.Http.AspNetCore`, ensuring all ASP.NET Core types resolve from the same shared runtime.
