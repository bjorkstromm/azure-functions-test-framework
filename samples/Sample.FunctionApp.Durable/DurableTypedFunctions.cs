using AzureFunctions.TestFramework.Durable;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using System.Text.Json.Serialization;

namespace Sample.FunctionApp.Durable;

/// <summary>
/// Durable functions that use complex typed inputs and outputs to verify that the fake
/// durable runner correctly serializes and deserializes data through activity/orchestration
/// boundaries, and that a custom <c>WorkerOptions.Serializer</c> does not break durable flows.
/// </summary>
public class DurableTypedFunctions
{
    /// <summary>
    /// Orchestrator that accepts a complex typed <see cref="TypedGreetingInput"/> and returns a
    /// complex typed <see cref="TypedGreetingOutput"/> after running an activity.
    /// </summary>
    [Function(nameof(RunTypedGreetingOrchestration))]
    public static async Task<TypedGreetingOutput> RunTypedGreetingOrchestration(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<TypedGreetingInput>() ?? new TypedGreetingInput("World", 1);
        var greeting = await context.CallActivityAsync<string>(nameof(CreateTypedGreeting), input.Name);
        return new TypedGreetingOutput(greeting, input.Name, input.RepeatCount);
    }

    /// <summary>
    /// Activity that creates a greeting string from a name.
    /// </summary>
    [Function(nameof(CreateTypedGreeting))]
    public static string CreateTypedGreeting([ActivityTrigger] string name) =>
        $"Hello, {name}!";
}

/// <summary>Complex input for the typed greeting orchestration.</summary>
public sealed record TypedGreetingInput(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("repeat_count")] int RepeatCount);

/// <summary>Complex output from the typed greeting orchestration.</summary>
public sealed record TypedGreetingOutput(
    [property: JsonPropertyName("greeting")] string Greeting,
    [property: JsonPropertyName("recipient")] string Recipient,
    [property: JsonPropertyName("repeat_count")] int RepeatCount);
