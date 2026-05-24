using AzureFunctions.TestFramework.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TestProject;

/// <summary>Tests for MCP-triggered functions (tool, resource, and prompt triggers).</summary>
public abstract class McpTestsBase : TestHostTestBase
{
    private InMemoryProcessedItemsService? _processedItems;

    protected McpTestsBase(ITestOutputHelper output) : base(output) { }

    protected override async Task<IFunctionsTestHost> CreateTestHostAsync()
    {
        _processedItems = new InMemoryProcessedItemsService();
        return await CreateTestHostWithProcessedItemsAsync(_processedItems);
    }

    protected abstract Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(InMemoryProcessedItemsService processedItems);

    [Fact]
    public async Task InvokeMcpToolAsync_WithNoArguments_Succeeds()
    {
        var result = await TestHost.InvokeMcpToolAsync("InvokeMcpTool", cancellationToken: TestCancellation);

        Assert.True(result.Success, $"MCP tool invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal("InvokeMcpTool", processed[0]);
    }

    [Fact]
    public async Task InvokeMcpToolAsync_WithToolName_UsesProvidedName()
    {
        var toolName = "my-test-tool";

        var result = await TestHost.InvokeMcpToolAsync(
            "InvokeMcpTool",
            toolName: toolName,
            cancellationToken: TestCancellation);

        Assert.True(result.Success, $"MCP tool invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(toolName, processed[0]);
    }

    [Fact]
    public async Task InvokeMcpResourceAsync_WithResourceUri_Succeeds()
    {
        var resourceUri = "resource://test/hello";

        var result = await TestHost.InvokeMcpResourceAsync("GetMcpResource", resourceUri, cancellationToken: TestCancellation);

        Assert.True(result.Success, $"MCP resource invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(resourceUri, processed[0]);
    }

    [Fact]
    public async Task InvokeMcpPromptAsync_WithNoArguments_Succeeds()
    {
        var result = await TestHost.InvokeMcpPromptAsync("InvokeMcpPrompt", cancellationToken: TestCancellation);

        Assert.True(result.Success, $"MCP prompt invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal("InvokeMcpPrompt", processed[0]);
    }

    [Fact]
    public async Task InvokeMcpPromptAsync_WithPromptName_UsesProvidedName()
    {
        var promptName = "my-test-prompt";

        var result = await TestHost.InvokeMcpPromptAsync(
            "InvokeMcpPrompt",
            promptName: promptName,
            cancellationToken: TestCancellation);

        Assert.True(result.Success, $"MCP prompt invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(promptName, processed[0]);
    }
}
