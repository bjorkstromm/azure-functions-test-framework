using Microsoft.Azure.Functions.Worker;

namespace Sample.FunctionApp.Durable;

/// <summary>
/// Represents this type.
/// </summary>
public sealed class InjectedGreetingActivityFunctions
{
    private readonly GreetingFormatter _formatter;

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public InjectedGreetingActivityFunctions(GreetingFormatter formatter)
    {
        _formatter = formatter;
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Function(nameof(CreateGreetingWithService))]
    public string CreateGreetingWithService([ActivityTrigger] string name)
    {
        return _formatter.Format(name);
    }
}

/// <summary>
/// Represents this type.
/// </summary>
public sealed class GreetingFormatter
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public string Format(string name) => $"Hello, {name}! (from service)";
}
