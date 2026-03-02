# Contributing to Azure Functions Test Framework

Thank you for your interest in contributing! This document provides guidelines and information for contributors.

## Current Status

**This framework is in early development.** The core infrastructure is complete, but there's a critical blocker preventing function invocation. See [KNOWN_ISSUES.md](KNOWN_ISSUES.md) for details.

## Priority: Function Loading/Discovery

**The #1 priority is solving the function loading issue.** All other work is blocked on this.

### The Problem
Functions aren't being discovered by the Azure Functions Worker, causing all invocations to fail with 500 errors.

**Symptoms**:
- Worker connects to gRPC server successfully ✅
- FunctionsMetadataRequest returns 0 functions ❌
- HTTP requests return 500 Internal Server Error ❌

**What We Need**:
- Understanding of how dotnet-isolated worker discovers functions
- How function metadata is generated at build time
- Whether Sample.FunctionApp needs special build configuration
- Alternative discovery mechanisms if metadata generation doesn't work

### Where to Start
1. Read [KNOWN_ISSUES.md](KNOWN_ISSUES.md) - Current blocker details
2. Read [.github/copilot-instructions.md](.github/copilot-instructions.md) - Architecture overview
3. Check Azure Functions Worker SDK source: https://github.com/Azure/azure-functions-dotnet-worker
   - Look at `Microsoft.Azure.Functions.Worker.Sdk.targets`
   - Check `DefaultFunctionMetadataProvider` implementation
4. Compare Sample.FunctionApp with a working Azure Functions project

### Testing Your Changes
```bash
# Build solution
dotnet build

# Run integration tests
dotnet test tests/Sample.FunctionApp.Tests

# Run single test with detailed logging
dotnet test --filter "GetTodos_ReturnsEmptyList" --logger "console;verbosity=detailed"

# Check if metadata files are generated
Get-ChildItem samples/Sample.FunctionApp/bin/Debug/net8.0 -Recurse -Filter "*.json"
```

## Development Setup

### Prerequisites
- .NET 8 SDK or later
- Visual Studio 2022 / VS Code / Rider
- Basic understanding of:
  - Azure Functions (dotnet-isolated model)
  - gRPC and Protocol Buffers
  - ASP.NET Core (helpful for understanding design goals)

### Building
```bash
# Clone the repository
git clone https://github.com/YOUR-USERNAME/azure-functions-test-framework
cd azure-functions-test-framework

# Restore and build
dotnet restore
dotnet build

# Run tests
dotnet test
```

## Project Structure
```
src/
  AzureFunctions.TestFramework.Core/    # Main framework package
    ├── Grpc/                            # gRPC server implementation
    ├── Worker/                          # In-process worker hosting  
    ├── Http/                            # HTTP request/response mapping
    ├── Client/                          # Custom HttpMessageHandler
    └── Protos/                          # Protocol Buffer definitions
    
  AzureFunctions.TestFramework.Http/    # HTTP-specific functionality
  
samples/
  Sample.FunctionApp/                   # Example Azure Functions app
  
tests/
  Sample.FunctionApp.Tests/             # Integration tests
```

## Key Files

### Core Framework
- `src/AzureFunctions.TestFramework.Core/FunctionsTestHost.cs` - Main orchestrator
- `src/AzureFunctions.TestFramework.Core/FunctionsTestHostBuilder.cs` - Fluent builder API
- `src/AzureFunctions.TestFramework.Core/Grpc/GrpcHostService.cs` - gRPC protocol handler
- `src/AzureFunctions.TestFramework.Core/Worker/WorkerHostService.cs` - Worker lifecycle management

### Critical for Function Loading
- `src/AzureFunctions.TestFramework.Core/Grpc/GrpcHostService.cs:HandleStartStreamAsync()` - Where function loading happens
- `samples/Sample.FunctionApp/Sample.FunctionApp.csproj` - Build configuration

## Coding Guidelines

### Code Style
- Use nullable reference types (`#nullable enable`)
- Add XML documentation for public APIs
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
feat: implement function metadata discovery via reflection

- Add DefaultFunctionMetadataProvider integration
- Use reflection to discover functions at runtime
- Remove dependency on .functions.json files

Fixes #1
```

## Getting Help

### Questions About Architecture
- Read [.github/copilot-instructions.md](.github/copilot-instructions.md)
- Check Azure Functions Worker SDK docs: https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide

### Questions About the Blocker
- Read [KNOWN_ISSUES.md](KNOWN_ISSUES.md) for detailed analysis
- Check existing GitHub Issues for related discussions
- Ask questions in a new GitHub Issue with the `question` label

### Debugging Tips
```bash
# Enable detailed gRPC logging
dotnet test --logger "console;verbosity=detailed" 2>&1 | Select-String -Pattern "Grpc"

# Check function metadata files
Get-ChildItem samples/Sample.FunctionApp/bin/Debug/net8.0 -Recurse

# Compare with working func.exe project
cd samples/Sample.FunctionApp
func start  # See what func.exe generates
```

## License
By contributing, you agree that your contributions will be licensed under the MIT License.

## Code of Conduct
- Be respectful and inclusive
- Focus on constructive feedback
- Help others learn and grow
- Keep discussions on-topic

Thank you for contributing! 🎉
