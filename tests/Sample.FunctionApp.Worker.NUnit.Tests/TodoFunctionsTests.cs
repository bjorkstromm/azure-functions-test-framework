using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Timer;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace Sample.FunctionApp.Worker.NUnit.Tests;

/// <summary>
/// Integration tests using <see cref="FunctionsTestHost"/> in direct gRPC mode with
/// <c>IHostBuilder</c>.  Inherits common tests from <see cref="TodoFunctionsCoreTestsBase"/>.
/// </summary>
[TestFixture]
public class TodoFunctionsTests : TodoFunctionsCoreTestsBase
{
    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .ConfigureServices(services => services.AddSingleton<ITodoService, InMemoryTodoService>())
            .BuildAndStartAsync();

    // ── Mode-specific tests ───────────────────────────────────────────────────

    [Test]
    public async Task CreateTodo_ReturnsTodo_WithGeneratedId()
    {
        var response = await Client!.PostAsJsonAsync("/api/todos", new { Title = "NUnit Task" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var todo = await response.Content.ReadFromJsonAsync<TodoItem>();
        Assert.That(todo, Is.Not.Null);
        Assert.That(todo!.Id, Is.Not.Null.And.Not.Empty);
        Assert.That(todo.Title, Is.EqualTo("NUnit Task"));
        Assert.That(todo.IsCompleted, Is.False);
    }

    [Test]
    public async Task UpdateTodo_UpdatesExistingTodo()
    {
        var createResponse = await Client!.PostAsJsonAsync("/api/todos", new { Title = "Original" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoItem>();

        var response = await Client!.PutAsJsonAsync(
            $"/api/todos/{created!.Id}",
            new { Title = "Updated", IsCompleted = true });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updated = await response.Content.ReadFromJsonAsync<TodoItem>();
        Assert.That(updated!.Title, Is.EqualTo("Updated"));
        Assert.That(updated.IsCompleted, Is.True);
    }

    [TestCase("GET", "probe", false)]
    [TestCase("HEAD", "", false)]
    [TestCase("OPTIONS", "", false)]
    [TestCase("PATCH", "PATCH", true)]
    public async Task HttpVerbsProbe_RoutesVerbAndExposesMethodHeader(string method, string expectedBody, bool requestBody)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/http-verbs-probe");
        if (requestBody)
        {
            request.Content = new StringContent(method);
        }

        var response = await Client!.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo(expectedBody));
        Assert.That(response.Headers.TryGetValues("X-Probe-Method", out var values), Is.True);
        Assert.That(values!.Single(), Is.EqualTo(method).IgnoreCase);
    }

    [Test]
    public async Task InvokeTimerAsync_WithDefaultTimerInfo_Succeeds()
    {
        var result = await TestHost!.InvokeTimerAsync("HeartbeatTimer");
        TestContext.Progress.WriteLine($"Success: {result.Success}, Error: {result.Error}");

        Assert.That(result.Success, Is.True, $"Timer invocation failed: {result.Error}");
    }
}

