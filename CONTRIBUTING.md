# Contributing to Azure Functions Test Framework

Thank you for your interest in contributing! This document provides guidelines and information for contributors.

## Current Status

This framework is in **preview (pre-1.0)**. The core infrastructure, all trigger packages, and the durable support package are fully functional. See [README.md](README.md) for the full capability matrix.

## Development Setup

### Prerequisites
- .NET 8 SDK and .NET 10 SDK
- Visual Studio 2022 / VS Code / Rider
- Basic understanding of:
  - Azure Functions (dotnet-isolated model)
  - gRPC and Protocol Buffers
  - ASP.NET Core (for WebApplicationFactory-based testing)

### Building
```bash
git clone https://github.com/bjorkstromm/azure-functions-test-framework
cd azure-functions-test-framework
dotnet restore
dotnet build
```

### Running Tests
```bash
# All tests
dotnet test

# Specific test suites
dotnet test tests/Sample.FunctionApp.Worker.Tests          # xUnit — gRPC
dotnet test tests/Sample.FunctionApp.Worker.WAF.Tests      # xUnit — WAF
dotnet test tests/Sample.FunctionApp.Worker.NUnit.Tests    # NUnit — gRPC
dotnet test tests/Sample.FunctionApp.Worker.WAF.NUnit.Tests # NUnit — WAF
dotnet test tests/Sample.FunctionApp.Durable.Tests         # Durable Functions
dotnet test tests/Sample.FunctionApp.CustomRoutePrefix.Tests # Custom route prefix

# Single test with detailed logging
dotnet test --filter "GetTodos_ReturnsEmptyList" --logger "console;verbosity=detailed"
```

## Architecture Overview

For a detailed architecture walkthrough, see [.github/copilot-instructions.md](.github/copilot-instructions.md). Key design decisions (ALC isolation, FrameworkReference, durable converter interception) are documented in README.md under "Architecture & Design Decisions".

### Key Files

- `src/AzureFunctions.TestFramework.Core/FunctionsTestHost.cs` — Main orchestrator
- `src/AzureFunctions.TestFramework.Core/FunctionsTestHostBuilder.cs` — Fluent builder API
- `src/AzureFunctions.TestFramework.Core/Grpc/GrpcHostService.cs` — gRPC protocol handler + route matching
- `src/AzureFunctions.TestFramework.Core/Worker/WorkerHostService.cs` — Worker lifecycle management
- `src/AzureFunctions.TestFramework.Core/Worker/InProcessMethodInfoLocator.cs` — ALC isolation fix
- `src/AzureFunctions.TestFramework.Http.AspNetCore/FunctionsWebApplicationFactory.cs` — WAF integration

## Coding Guidelines

### Code Style
- Use nullable reference types (`#nullable enable`)
- Add XML documentation for public APIs (the build enforces `TreatWarningsAsErrors=true`)
- Follow existing patterns (e.g., async/await, ILogger usage)
- Use meaningful variable names

### gRPC Event Stream
**Important**: Never block the gRPC event stream. Use `Task.Run()` for long-running operations:

```csharp
// ❌ DON'T: Blocks event stream
var response = await SendMessageAsync(request);

// ✅ DO: Run in background
_ = Task.Run(async () => {
    var response = await SendMessageAsync(request);
}, cancellationToken);
```

### NuGet / Versioning
- Package versions are managed centrally via `Directory.Packages.props` — add new versions there, not inline
- Semantic versioning is driven by MinVer from git tags (`v*.*.*`)

### Testing
- Add unit tests for new functionality
- Update integration tests if changing public API
- Ensure existing tests still pass
- Add test cases for edge cases

## Pull Request Process

1. **Fork** the repository
2. **Create a branch** from `main`: `git checkout -b feature/your-feature-name`
3. **Make your changes** following the coding guidelines
4. **Add/update tests** as appropriate
5. **Ensure all tests pass**: `dotnet test`
6. **Commit your changes** with clear commit messages
7. **Push** to your fork
8. **Create a Pull Request** with:
   - Clear description of what problem you're solving
   - Reference to any related issues
   - Test results showing your changes work

## Commit Message Format
```
<type>: <subject>

<body>

<footer>
```

**Types**:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `test`: Adding/updating tests
- `refactor`: Code refactoring
- `chore`: Maintenance tasks

**Example**:
```
feat: add Cosmos DB trigger invocation support

- Add AzureFunctions.TestFramework.CosmosDb package
- Implement InvokeCosmosDbAsync extension method
- Add sample function and integration tests

Fixes #42
```

## Getting Help

- Read [README.md](README.md) for capabilities and usage examples
- Read [.github/copilot-instructions.md](.github/copilot-instructions.md) for detailed architecture
- Check [KNOWN_ISSUES.md](KNOWN_ISSUES.md) for active caveats
- Check Azure Functions Worker SDK docs: https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide
- Ask questions in a new GitHub Issue with the `question` label

## License
By contributing, you agree that your contributions will be licensed under the MIT License.

## Code of Conduct
- Be respectful and inclusive
- Focus on constructive feedback
- Help others learn and grow
- Keep discussions on-topic

Thank you for contributing! 🎉
