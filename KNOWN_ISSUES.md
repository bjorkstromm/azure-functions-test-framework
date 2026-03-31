# Known Issues

For current capabilities, package layout, and common commands, see `README.md`. This file is intentionally limited to active blockers and known issues.

## Active blockers

- No active blockers for the current Worker SDK 2.x sample/test suites.

## Known issues

- `FunctionsTestHostBuilder.ConfigureEnvironmentVariable(name, value)` sets process-level environment variables, so tests that need different values for the same variable name should not run in parallel.
- The durable support package currently uses a framework-owned fake path (`ConfigureFakeDurableSupport(...)` + `FunctionsDurableClientProvider`) instead of the real Durable runtime and execution engine.
- `AzureFunctions.TestFramework.Http` is still a minimal package with little public surface area today; it is packed/published so the package set stays stable while HTTP-specific helpers are added later.

## Resolved issues

- **`HttpContextConverter` unable to read `HttpRequest` from `FunctionContext`** — When using the test framework in a project where the function app declares `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, `HttpContextConverter.GetHttpContext()` could silently return `null` (the `as HttpContext` cast fails) because two physical copies of `Microsoft.AspNetCore.Http.Abstractions.dll` could be loaded in the same process — one from the shared framework (functions app) and one via the NuGet package chain (test framework). Fixed by adding an explicit `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to `AzureFunctions.TestFramework.Core` and `AzureFunctions.TestFramework.Http.AspNetCore`, ensuring all ASP.NET Core types resolve from the same shared runtime.
