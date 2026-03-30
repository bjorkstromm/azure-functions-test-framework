using AzureFunctions.TestFramework.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sample.FunctionApp.Worker;
using System.Net.Http.Json;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Sample.FunctionApp.Worker.Tests;

public class FunctionsTestHostFeaturesTests
{
    private readonly ITestOutputHelper _output;

    public FunctionsTestHostFeaturesTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private ILoggerFactory CreateLoggerFactory() =>
        LoggerFactory.Create(b => b.AddProvider(new XUnitLoggerProvider(_output)));
    [Fact]
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
        Assert.Same(seededService, resolvedService);

        using var client = testHost.CreateHttpClient();
        var todos = await client.GetFromJsonAsync<List<TodoItem>>("/api/todos");

        Assert.NotNull(todos);
        await Verify(todos);
    }

    [Fact]
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
        Assert.Same(seededService, resolvedService);

        using var client = testHost.CreateHttpClient();
        var todos = await client.GetFromJsonAsync<List<TodoItem>>("/api/todos");

        Assert.NotNull(todos);
        await Verify(todos);
    }

    [Fact]
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
        Assert.Equal("configured-value", configuration["Demo:Message"]);

        using var client = testHost.CreateHttpClient();
        var payload = await client.GetFromJsonAsync<ConfigurationValueResponse>("/api/config/Demo:Message");

        Assert.NotNull(payload);
        await Verify(payload);
    }

    [Fact]
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
        Assert.Equal(envVarValue, configuration[envVarName]);

        using var client = testHost.CreateHttpClient();
        var payload = await client.GetFromJsonAsync<ConfigurationValueResponse>(
            $"/api/config/{Uri.EscapeDataString(envVarName)}");

        Assert.NotNull(payload);
        Assert.Equal(envVarValue, payload!.Value);
    }

    private sealed class SeededTodoService : ITodoService
    {
        private readonly List<TodoItem> _todos;

        public SeededTodoService(params TodoItem[] seed) => _todos = new List<TodoItem>(seed);

        public Task<IEnumerable<TodoItem>> GetAllAsync() => Task.FromResult<IEnumerable<TodoItem>>(_todos);
        public Task<TodoItem?> GetByIdAsync(string id) => Task.FromResult(_todos.FirstOrDefault(t => t.Id == id));

        public Task<TodoItem> CreateAsync(TodoItem item)
        {
            item.Id = Guid.NewGuid().ToString();
            _todos.Add(item);
            return Task.FromResult(item);
        }

        public Task<TodoItem?> UpdateAsync(string id, TodoItem updates)
        {
            var existing = _todos.FirstOrDefault(t => t.Id == id);
            if (existing == null) return Task.FromResult<TodoItem?>(null);
            existing.Title = updates.Title;
            existing.IsCompleted = updates.IsCompleted;
            return Task.FromResult<TodoItem?>(existing);
        }

        public Task<bool> DeleteAsync(string id)
        {
            var todo = _todos.FirstOrDefault(t => t.Id == id);
            if (todo == null) return Task.FromResult(false);
            _todos.Remove(todo);
            return Task.FromResult(true);
        }
    }
}
