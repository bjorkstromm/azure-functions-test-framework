namespace Sample.FunctionApp.WebApplicationFactory.Tests;

/// <summary>
/// Integration tests that use <see cref="FunctionsWebApplicationFactory{TProgram}"/> — a
/// <c>WebApplicationFactory</c>-based approach that runs the Azure Functions app through its
/// full ASP.NET Core pipeline (including middleware and services from <c>Program.cs</c>).
/// </summary>
public class FunctionsWebApplicationFactoryTests : IClassFixture<FunctionsWebApplicationFactory<Program>>
{
    private readonly FunctionsWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public FunctionsWebApplicationFactoryTests(
        FunctionsWebApplicationFactory<Program> factory,
        ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetTodos_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/api/todos");

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Content: {await response.Content.ReadAsStringAsync()}");

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Health_ReturnsHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        _output.WriteLine($"Status Code: {response.StatusCode}");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateAndGetTodo_WorksEndToEnd()
    {
        // Arrange
        var newTodo = new { Title = "WebApplicationFactory Test" };

        // Act – create
        var createResponse = await _client.PostAsJsonAsync("/api/todos", newTodo);

        _output.WriteLine($"Create Status: {createResponse.StatusCode}");

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<TodoItem>();
        Assert.NotNull(created);
        Assert.Equal("WebApplicationFactory Test", created.Title);

        // Act – retrieve
        var getResponse = await _client.GetAsync($"/api/todos/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<TodoItem>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
    }

    [Fact]
    public async Task WithWebHostBuilder_CanOverrideServices()
    {
        // Demonstrates the WebApplicationFactory pattern: swap services for test doubles
        // by creating a customised client via WithWebHostBuilder.
        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(_ =>
            {
                // Services can be overridden here (e.g. replace a real DB with an in-memory one).
                // Nothing to override in this sample, but the pattern is demonstrated.
            });
        });

        using var customClient = customFactory.CreateClient();
        var response = await customClient.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();
    }
}
