using System.Text.Json;
using AzureFunctions.TestFramework.Core;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace AzureFunctions.TestFramework.Mcp;

/// <summary>
/// Extension methods for invoking MCP-triggered Azure Functions via <see cref="IFunctionsTestHost"/>.
/// </summary>
public static class FunctionsTestHostMcpExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Invokes an MCP tool–triggered function by name with the specified tool arguments.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the MCP tool function (case-insensitive).</param>
    /// <param name="toolArguments">
    /// The named arguments to pass to the tool, or <see langword="null"/> for no arguments.
    /// </param>
    /// <param name="toolName">
    /// The tool name to include in the invocation context.
    /// Defaults to <paramref name="functionName"/> when <see langword="null"/>.
    /// </param>
    /// <param name="sessionId">
    /// The MCP session ID to include in the invocation context.
    /// A new <see cref="Guid"/> is used when <see langword="null"/>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeMcpToolAsync(
        this IFunctionsTestHost host,
        string functionName,
        IReadOnlyDictionary<string, object>? toolArguments = null,
        string? toolName = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var context = new ToolInvocationContext
        {
            Name = toolName ?? functionName,
            Arguments = toolArguments?.ToDictionary(k => k.Key, k => k.Value)
                        ?? new Dictionary<string, object>(),
            SessionId = sessionId ?? Guid.NewGuid().ToString()
        };

        var json = JsonSerializer.Serialize(context, _jsonOptions);

        var invocationContext = new FunctionInvocationContext
        {
            TriggerType = "mcpToolTrigger",
            InputData = { ["$mcpToolJson"] = json }
        };

        return host.Invoker.InvokeAsync(functionName, invocationContext, CreateToolBindingData, cancellationToken);
    }

    /// <summary>
    /// Invokes an MCP resource–triggered function by name with the specified resource URI.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the MCP resource function (case-insensitive).</param>
    /// <param name="resourceUri">The URI of the MCP resource to simulate as the trigger input.</param>
    /// <param name="sessionId">
    /// The MCP session ID to include in the invocation context.
    /// A new <see cref="Guid"/> is used when <see langword="null"/>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeMcpResourceAsync(
        this IFunctionsTestHost host,
        string functionName,
        string resourceUri,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resourceUri);

        var context = new ResourceInvocationContext(resourceUri)
        {
            SessionId = sessionId ?? Guid.NewGuid().ToString()
        };

        var json = JsonSerializer.Serialize(context, _jsonOptions);

        var invocationContext = new FunctionInvocationContext
        {
            TriggerType = "mcpResourceTrigger",
            InputData = { ["$mcpResourceJson"] = json }
        };

        return host.Invoker.InvokeAsync(functionName, invocationContext, CreateResourceBindingData, cancellationToken);
    }

    /// <summary>
    /// Invokes an MCP prompt–triggered function by name with the specified prompt arguments.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the MCP prompt function (case-insensitive).</param>
    /// <param name="arguments">
    /// The named arguments to pass to the prompt, or <see langword="null"/> for no arguments.
    /// </param>
    /// <param name="promptName">
    /// The prompt name to include in the invocation context.
    /// Defaults to <paramref name="functionName"/> when <see langword="null"/>.
    /// </param>
    /// <param name="sessionId">
    /// The MCP session ID to include in the invocation context.
    /// A new <see cref="Guid"/> is used when <see langword="null"/>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeMcpPromptAsync(
        this IFunctionsTestHost host,
        string functionName,
        IReadOnlyDictionary<string, string>? arguments = null,
        string? promptName = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var context = new PromptInvocationContext
        {
            Name = promptName ?? functionName,
            Arguments = arguments?.ToDictionary(k => k.Key, k => k.Value)
                        ?? new Dictionary<string, string>(),
            SessionId = sessionId ?? Guid.NewGuid().ToString()
        };

        var json = JsonSerializer.Serialize(context, _jsonOptions);

        var invocationContext = new FunctionInvocationContext
        {
            TriggerType = "mcpPromptTrigger",
            InputData = { ["$mcpPromptJson"] = json }
        };

        return host.Invoker.InvokeAsync(functionName, invocationContext, CreatePromptBindingData, cancellationToken);
    }

    private static TriggerBindingData CreateToolBindingData(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var json = context.InputData.TryGetValue("$mcpToolJson", out var j) ? j?.ToString() ?? "{}" : "{}";

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithJson(function.ParameterName, json)],
            TriggerMetadataJson = new Dictionary<string, string>(StringComparer.Ordinal)
                { [function.ParameterName] = json }
        };
    }

    private static TriggerBindingData CreateResourceBindingData(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var json = context.InputData.TryGetValue("$mcpResourceJson", out var j) ? j?.ToString() ?? "{}" : "{}";

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithJson(function.ParameterName, json)],
            TriggerMetadataJson = new Dictionary<string, string>(StringComparer.Ordinal)
                { [function.ParameterName] = json }
        };
    }

    private static TriggerBindingData CreatePromptBindingData(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var json = context.InputData.TryGetValue("$mcpPromptJson", out var j) ? j?.ToString() ?? "{}" : "{}";

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithJson(function.ParameterName, json)],
            TriggerMetadataJson = new Dictionary<string, string>(StringComparer.Ordinal)
                { [function.ParameterName] = json }
        };
    }
}

