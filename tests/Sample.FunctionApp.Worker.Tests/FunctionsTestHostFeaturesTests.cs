using AzureFunctions.TestFramework.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sample.FunctionApp.Worker;
using System.Net.Http.Json;
using Xunit;

namespace Sample.FunctionApp.Worker.Tests;

public class FunctionsTestHostFeaturesTests
{
    [Fact]
    public async Task Services_ReturnsConfiguredSingletonService()
    {
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
            .ConfigureServices(services => services.AddSingleton<ITodoService>(seededService))
            .BuildAndStartAsync();

        var resolvedService = testHost.Services.GetRequiredService<ITodoService>();
        Assert.Same(seededService, resolvedService);

        using var client = testHost.CreateHttpClient();
        var todos = await client.GetFromJsonAsync<List<TodoItem>>("/api/todos");

        Assert.NotNull(todos);
        Assert.Single(todos);
        Assert.Equal("services-seeded-id", todos[0].Id);
    }

    [Fact]
    public async Task WithHostBuilderFactory_ConfigureServices_CanOverrideServices()
    {
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
            .WithHostBuilderFactory(Program.CreateWorkerHostBuilder)
            .ConfigureServices(services => services.AddSingleton<ITodoService>(seededService))
            .BuildAndStartAsync();

        var resolvedService = testHost.Services.GetRequiredService<ITodoService>();
        Assert.Same(seededService, resolvedService);

        using var client = testHost.CreateHttpClient();
        var todos = await client.GetFromJsonAsync<List<TodoItem>>("/api/todos");

        Assert.NotNull(todos);
        Assert.Single(todos);
        Assert.Equal("override-seeded-id", todos[0].Id);
    }

    [Fact]
    public async Task ConfigureSetting_AddsConfigurationOverride()
    {
        await using var testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithHostBuilderFactory(Program.CreateWorkerHostBuilder)
            .ConfigureSetting("Demo:Message", "configured-value")
            .BuildAndStartAsync();

        var configuration = testHost.Services.GetRequiredService<IConfiguration>();
        Assert.Equal("configured-value", configuration["Demo:Message"]);

        using var client = testHost.CreateHttpClient();
        var payload = await client.GetFromJsonAsync<ConfigurationValueResponse>("/api/config/Demo:Message");

        Assert.NotNull(payload);
        Assert.Equal("Demo:Message", payload.Key);
        Assert.Equal("configured-value", payload.Value);
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
