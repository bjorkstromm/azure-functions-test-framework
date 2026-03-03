using AzureFunctions.TestFramework.AspNetCore;
using Xunit;
using Xunit.Abstractions;

namespace Sample.FunctionApp.WebApplicationFactory.Tests;

public class DebugRouteMapTest : IClassFixture<FunctionsWebApplicationFactory<Program>>
{
    private readonly FunctionsWebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;
    
    public DebugRouteMapTest(FunctionsWebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }
    
    [Fact]
    public void ShowRouteMap()
    {
        foreach (var entry in _factory.FunctionRouteMap)
        {
            _output.WriteLine($"Route: {entry.Key} -> FunctionId: {entry.Value}");
        }
        Assert.True(_factory.FunctionRouteMap.Count > 0, "Route map should not be empty");
    }
}
