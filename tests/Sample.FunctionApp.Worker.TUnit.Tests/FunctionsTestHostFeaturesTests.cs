using System.Reflection;
using AzureFunctions.TestFramework.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sample.FunctionApp.Worker;
using System.Net.Http.Json;
using TUnit.Core;

namespace Sample.FunctionApp.Worker.TUnit.Tests;

/// <summary>
/// Tests for <see cref="FunctionsTestHostBuilder"/> configuration and DI behavior.
/// </summary>
public class FunctionsTestHostFeaturesTests
{
    private static ILoggerFactory CreateLoggerFactory() =>
        LoggerFactory.Create(b => b.AddProvider(new TUnitLoggerProvider()));

    [Test]
    public async Task Services_ReturnsConfiguredSingletonService()
    {
        // Arrange
        var seededTodo = new TodoItem
        {
            Id = "services-seeded-id",
            Title = "Seeded via Services",
            IsCompleted = false,
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var seededService = new SeededTodoService(seededTodo);

        await using var testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .ConfigureServices(services => services.AddSingleton<ITodoService>(seededService))
            .BuildAndStartAsync();

        // Act
        var resolvedService = testHost.Services.GetRequiredService<ITodoService>();

        // Assert
        await Assert.That(ReferenceEquals(seededService, resolvedService)).IsTrue();

        using var client = testHost.CreateHttpClient();
        var todos = await client.GetFromJsonAsync<List<TodoItem>>("/api/todos");

        await Assert.That(todos).IsNotNull();
        await Verify(todos!);
    }

    [Test]
    public async Task WithHostBuilderFactory_ConfigureServices_CanOverrideServices()
    {
        // Arrange
        var seededTodo = new TodoItem
        {
            Id = "override-seeded-id",
            Title = "Override from ConfigureServices",
            IsCompleted = false,
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var seededService = new SeededTodoService(seededTodo);

        await using var testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(Program.CreateWorkerHostBuilder)
            .ConfigureServices(services => services.AddSingleton<ITodoService>(seededService))
            .BuildAndStartAsync();

        // Act
        var resolvedService = testHost.Services.GetRequiredService<ITodoService>();

        // Assert
        await Assert.That(ReferenceEquals(seededService, resolvedService)).IsTrue();

        using var client = testHost.CreateHttpClient();
        var todos = await client.GetFromJsonAsync<List<TodoItem>>("/api/todos");

        await Assert.That(todos).IsNotNull();
        await Verify(todos!);
    }

    [Test]
    public async Task ConfigureSetting_AddsConfigurationOverride()
    {
        // Arrange
        await using var testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(Program.CreateWorkerHostBuilder)
            .ConfigureSetting("Demo:Message", "configured-value")
            .BuildAndStartAsync();

        // Act
        var configuration = testHost.Services.GetRequiredService<IConfiguration>();

        // Assert
        await Assert.That(configuration["Demo:Message"]).IsEqualTo("configured-value");

        using var client = testHost.CreateHttpClient();
        var payload = await client.GetFromJsonAsync<ConfigurationValueResponse>("/api/config/Demo:Message");

        await Assert.That(payload).IsNotNull();
        await Verify(payload!);
    }

    [Test]
    public async Task ConfigureEnvironmentVariable_SetsEnvironmentVariableVisibleToFunction()
    {
        // Arrange
        var envVarName = $"TEST_ENV_{Guid.NewGuid():N}";
        const string envVarValue = "env-var-value";

        await using var testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(Program.CreateWorkerHostBuilder)
            .ConfigureEnvironmentVariable(envVarName, envVarValue)
            .BuildAndStartAsync();

        // Act
        var configuration = testHost.Services.GetRequiredService<IConfiguration>();

        // Assert
        await Assert.That(configuration[envVarName]).IsEqualTo(envVarValue);

        using var client = testHost.CreateHttpClient();
        var payload = await client.GetFromJsonAsync<ConfigurationValueResponse>(
            $"/api/config/{Uri.EscapeDataString(envVarName)}");

        await Assert.That(payload).IsNotNull();
        await Assert.That(payload!.Value).IsEqualTo(envVarValue);
    }

    [Test]
    public async Task InProcessMethodInfoLocator_PreventsAssemblyDualLoading()
    {
        // Arrange & Act: start a host and let functions load
        await using var testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(Program.CreateWorkerHostBuilder)
            .BuildAndStartAsync();

        // Assert: no assembly name appears more than once in the AppDomain
        // (excluding dynamic, resource, and test-runner assemblies).
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Where(a => !IsTestRunnerAssembly(a.GetName().Name))
            .GroupBy(a => a.GetName().Name)
            .Where(g => g.Count() > 1)
            .Select(g => new { Name = g.Key, Count = g.Count(), Paths = g.Select(a => a.Location).ToList() })
            .ToList();

        foreach (var dup in loadedAssemblies)
        {
            TestContext.Current?.Output.WriteLine(
                $"DUPLICATE: {dup.Name} loaded {dup.Count}x from: {string.Join(", ", dup.Paths)}");
        }

        await Assert.That(loadedAssemblies.Count).IsEqualTo(0);
    }

    private static bool IsTestRunnerAssembly(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        return name.StartsWith("xunit", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("TUnit", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("nunit", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Microsoft.Testing", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("testhost", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class SeededTodoService : ITodoService
    {
        private readonly List<TodoItem> _todos;

        public SeededTodoService(params TodoItem[] seed) => _todos = new List<TodoItem>(seed);

        /// <inheritdoc />
        public Task<IEnumerable<TodoItem>> GetAllAsync() => Task.FromResult<IEnumerable<TodoItem>>(_todos);

        /// <inheritdoc />
        public Task<TodoItem?> GetByIdAsync(string id) => Task.FromResult(_todos.FirstOrDefault(t => t.Id == id));

        /// <inheritdoc />
        public Task<TodoItem> CreateAsync(TodoItem item)
        {
            item.Id = Guid.NewGuid().ToString();
            _todos.Add(item);
            return Task.FromResult(item);
        }

        /// <inheritdoc />
        public Task<TodoItem?> UpdateAsync(string id, TodoItem updates)
        {
            var existing = _todos.FirstOrDefault(t => t.Id == id);
            if (existing == null)
            {
                return Task.FromResult<TodoItem?>(null);
            }

            existing.Title = updates.Title;
            existing.IsCompleted = updates.IsCompleted;
            return Task.FromResult<TodoItem?>(existing);
        }

        /// <inheritdoc />
        public Task<bool> DeleteAsync(string id)
        {
            var todo = _todos.FirstOrDefault(t => t.Id == id);
            if (todo == null)
            {
                return Task.FromResult(false);
            }

            _todos.Remove(todo);
            return Task.FromResult(true);
        }
    }
}
