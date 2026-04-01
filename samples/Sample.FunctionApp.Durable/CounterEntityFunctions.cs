using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Entities;

namespace Sample.FunctionApp.Durable;

/// <summary>
/// A simple counter durable entity that supports add, reset, and get operations.
/// </summary>
public sealed class Counter : TaskEntity<int>
{
    /// <summary>Adds the given amount to the counter.</summary>
    public void Add(int amount) => State += amount;

    /// <summary>Resets the counter to zero.</summary>
    public void Reset() => State = 0;

    /// <summary>Returns the current counter value.</summary>
    public int Get() => State;

    /// <summary>Azure Functions entry point for the Counter entity.</summary>
    [Function(nameof(Counter))]
    public Task Run([EntityTrigger] TaskEntityDispatcher dispatcher) => dispatcher.DispatchAsync<Counter>();
}
