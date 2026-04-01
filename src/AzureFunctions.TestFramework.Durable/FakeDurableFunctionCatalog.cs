using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Entities;

namespace AzureFunctions.TestFramework.Durable;

internal sealed class FakeDurableFunctionCatalog
{
    private readonly Dictionary<string, FakeDurableFunctionDescriptor> _activities =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Type> _entities =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FakeDurableFunctionDescriptor> _orchestrators =
        new(StringComparer.OrdinalIgnoreCase);

    public FakeDurableFunctionCatalog(Assembly functionsAssembly)
    {
        // Build a map of ITaskEntity implementations by class name for entity resolution.
        var entityImplementations = functionsAssembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(ITaskEntity).IsAssignableFrom(t))
            .ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

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

                if (method.GetParameters().Any(parameter => parameter.GetCustomAttribute<EntityTriggerAttribute>() is not null))
                {
                    // By convention the entity class name matches the function name. Fall back to the
                    // declaring class itself if it directly implements ITaskEntity.
                    Type? entityType = null;
                    if (entityImplementations.TryGetValue(functionAttribute.Name, out entityType) ||
                        (typeof(ITaskEntity).IsAssignableFrom(type) && !type.IsAbstract && (entityType = type) is not null))
                    {
                        _entities[functionAttribute.Name] = entityType;
                    }
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

    public Type GetEntityType(string entityName)
    {
        if (_entities.TryGetValue(entityName, out var entityType))
        {
            return entityType;
        }

        throw new InvalidOperationException(
            $"Durable entity '{entityName}' was not found in the functions assembly. " +
            $"Ensure the entity function has an [EntityTrigger] parameter and the entity class " +
            $"implements ITaskEntity with the same name as the function.");
    }
}

internal sealed record FakeDurableFunctionDescriptor(string FunctionName, MethodInfo Method);
