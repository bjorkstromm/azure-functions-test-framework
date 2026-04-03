using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Timer;
using Microsoft.Extensions.DependencyInjection;

namespace Sample.FunctionApp.Worker.Tests;

/// <summary>
/// Integration tests for the Worker SDK 2.x sample app using <see cref="FunctionsTestHost"/>
/// in direct gRPC mode (<c>ConfigureFunctionsWorkerDefaults</c>) with <c>IHostBuilder</c>.
/// Inherits common tests from <see cref="TodoFunctionsCoreTestsBase"/>.
/// </summary>
public class TodoFunctionsTests : TodoFunctionsCoreTestsBase
{
    public TodoFunctionsTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(CreateLoggerFactory(Output))
            .ConfigureServices(services => services.AddSingleton<ITodoService, InMemoryTodoService>())
            .BuildAndStartAsync(TestCancellation);

    // ── Mode-specific tests ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateTodo_ReturnsTodo_WithGeneratedId()
    {
        var response = await Client!.PostAsJsonAsync("/api/todos", new { Title = "Test Task" }, TestCancellation);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);
        Assert.NotNull(todo);
        await Verify(todo);
    }

    [Fact]
    public async Task UpdateTodo_UpdatesExistingTodo()
    {
        var createResponse = await Client!.PostAsJsonAsync("/api/todos", new { Title = "Original" }, TestCancellation);
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);

        var response = await Client!.PutAsJsonAsync(
            $"/api/todos/{created!.Id}",
            new { Title = "Updated", IsCompleted = true },
            TestCancellation);

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);
        await Verify(updated);
    }

    [Fact]
    public async Task GetTodoAlt_ReturnsTodo_WhenHttpTriggerParamNameIsNotReq()
    {
        var createResponse = await Client!.PostAsJsonAsync("/api/todos", new { Title = "Alt Binding Test" }, TestCancellation);
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);

        var response = await Client!.GetAsync($"/api/todos/{created!.Id}/alt", TestCancellation);

        response.EnsureSuccessStatusCode();
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);
        Assert.Equal(created.Id, todo!.Id);
        Assert.Equal("Alt Binding Test", todo.Title);
    }

    [Theory]
    [InlineData("GET", "probe", false)]
    [InlineData("HEAD", "", false)]
    [InlineData("OPTIONS", "", false)]
    [InlineData("PATCH", "PATCH", true)]
    public async Task HttpVerbsProbe_RoutesVerbAndExposesMethodHeader(string method, string expectedBody, bool requestBody)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/http-verbs-probe");
        if (requestBody)
        {
            request.Content = new StringContent(method);
        }

        var response = await Client!.SendAsync(request, TestCancellation);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestCancellation);
        Assert.Equal(expectedBody, body);
        Assert.True(response.Headers.TryGetValues("X-Probe-Method", out var values));
        Assert.Equal(method, Assert.Single(values), ignoreCase: true);
    }

    [Fact]
    public async Task InvokeTimerAsync_WithDefaultTimerInfo_Succeeds()
    {
        var result = await TestHost!.InvokeTimerAsync("HeartbeatTimer", cancellationToken: TestCancellation);
        Output.WriteLine($"Success: {result.Success}, Error: {result.Error}");

        Assert.True(result.Success, $"Timer invocation failed: {result.Error}");
    }
}

public class TodoDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}

