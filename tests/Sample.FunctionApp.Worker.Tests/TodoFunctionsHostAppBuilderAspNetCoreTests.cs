using AzureFunctions.TestFramework.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sample.FunctionApp.Worker;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using Xunit.Abstractions;

namespace Sample.FunctionApp.Worker.Tests;

/// <summary>
/// Integration tests for the Worker SDK 2.x sample using <see cref="FunctionsTestHost"/> in
/// ASP.NET Core / Kestrel mode with <c>IHostApplicationBuilder</c>
/// (<c>FunctionsApplication.CreateBuilder</c> + <c>ConfigureFunctionsWebApplication</c>).
/// Uses <c>Program.CreateHostApplicationBuilder</c> so the worker is bootstrapped via the
/// modern minimal-hosting API and requests are routed through the full ASP.NET Core middleware
/// pipeline.
/// Inherits common tests from <see cref="TodoFunctionsCoreTestsBase"/>.
/// </summary>
public class TodoFunctionsHostAppBuilderAspNetCoreTests : TodoFunctionsCoreTestsBase
{
    public TodoFunctionsHostAppBuilderAspNetCoreTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(CreateLoggerFactory(Output))
            .WithHostApplicationBuilderFactory(Program.CreateHostApplicationBuilder)
            .BuildAndStartAsync();

    // ── Mode-specific tests ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateTodo_ReturnsTodo_WithGeneratedId()
    {
        var response = await Client!.PostAsJsonAsync("/api/todos", new { Title = "HostAppBuilder ASP.NET Core Task" });
        Output.WriteLine($"Status: {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>();
        Assert.NotNull(todo);
        Assert.False(string.IsNullOrEmpty(todo!.Id.ToString()));
        Assert.Equal("HostAppBuilder ASP.NET Core Task", todo.Title);
        Assert.False(todo.IsCompleted);
    }

    [Fact]
    public async Task UpdateTodo_UpdatesExistingTodo()
    {
        var createResponse = await Client!.PostAsJsonAsync("/api/todos", new { Title = "Original" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>();

        var response = await Client!.PutAsJsonAsync(
            $"/api/todos/{created!.Id}",
            new { Title = "Updated", IsCompleted = true });

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<TodoDto>();
        Assert.Equal("Updated", updated!.Title);
        Assert.True(updated.IsCompleted);
    }

    [Fact]
    public async Task ConfigureServices_CanOverrideServicesInKestrelMode()
    {
        var seededTodo = new TodoItem
        {
            Id = "hostappbuilder-aspnetcore-seeded-id",
            Title = "HostAppBuilder ASP.NET Core Seeded Todo",
            IsCompleted = false,
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        await using var overrideHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(CreateLoggerFactory(Output))
            .WithHostApplicationBuilderFactory(Program.CreateHostApplicationBuilder)
            .ConfigureServices(services =>
            {
                services.RemoveAll<ITodoService>();
                services.AddSingleton<ITodoService>(new SeededTodoService(seededTodo));
            })
            .BuildAndStartAsync();

        using var customClient = overrideHost.CreateHttpClient();

        var response = await customClient.GetAsync("/api/todos");

        response.EnsureSuccessStatusCode();
        var todos = await response.Content.ReadFromJsonAsync<List<TodoItem>>();
        Assert.NotNull(todos);
        Assert.Single(todos);
        Assert.Equal("HostAppBuilder ASP.NET Core Seeded Todo", todos![0].Title);
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
