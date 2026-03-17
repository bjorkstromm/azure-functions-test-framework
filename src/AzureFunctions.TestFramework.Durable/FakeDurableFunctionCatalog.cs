using System.Reflection;
using Microsoft.Azure.Functions.Worker;

namespace AzureFunctions.TestFramework.Durable;

internal sealed class FakeDurableFunctionCatalog
{
    private readonly Dictionary<string, FakeDurableFunctionDescriptor> _activities =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FakeDurableFunctionDescriptor> _orchestrators =
        new(StringComparer.OrdinalIgnoreCase);

    public FakeDurableFunctionCatalog(Assembly functionsAssembly)
    {
        foreach (var type in functionsAssembly.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                var functionAttribute = method.GetCustomAttribute<FunctionAttribute>();
                if (functionAttribute is null)
                {
                    continue;
                }

                if (method.GetParameters().Any(parameter => parameter.GetCustomAttribute<ActivityTriggerAttribute>() is not null))
                {
                    _activities[functionAttribute.Name] = new FakeDurableFunctionDescriptor(functionAttribute.Name, method);
                }

                if (method.GetParameters().Any(parameter => parameter.GetCustomAttribute<OrchestrationTriggerAttribute>() is not null))
                {
                    _orchestrators[functionAttribute.Name] = new FakeDurableFunctionDescriptor(functionAttribute.Name, method);
                }
            }
        }
    }

    public FakeDurableFunctionDescriptor GetActivity(string name)
    {
        if (_activities.TryGetValue(name, out var descriptor))
        {
            return descriptor;
        }

        throw new InvalidOperationException($"Durable activity '{name}' was not found in the functions assembly.");
    }

    public FakeDurableFunctionDescriptor GetOrchestrator(string name)
    {
        if (_orchestrators.TryGetValue(name, out var descriptor))
        {
            return descriptor;
        }

        throw new InvalidOperationException($"Durable orchestrator '{name}' was not found in the functions assembly.");
    }
}

internal sealed record FakeDurableFunctionDescriptor(string FunctionName, MethodInfo Method);
