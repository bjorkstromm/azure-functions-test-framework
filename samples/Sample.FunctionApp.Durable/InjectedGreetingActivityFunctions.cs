using Microsoft.Azure.Functions.Worker;

namespace Sample.FunctionApp.Durable;

public sealed class InjectedGreetingActivityFunctions
{
    private readonly GreetingFormatter _formatter;

    public InjectedGreetingActivityFunctions(GreetingFormatter formatter)
    {
        _formatter = formatter;
    }

    [Function(nameof(CreateGreetingWithService))]
    public string CreateGreetingWithService([ActivityTrigger] string name)
    {
        return _formatter.Format(name);
    }
}

public sealed class GreetingFormatter
{
    public string Format(string name) => $"Hello, {name}! (from service)";
}
