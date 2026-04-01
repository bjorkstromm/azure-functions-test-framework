using Microsoft.DurableTask.Entities;

namespace AzureFunctions.TestFramework.Durable;

internal sealed class FakeTaskEntityOperation : TaskEntityOperation
{
    private readonly object? _input;

    public FakeTaskEntityOperation(string name, object? input, TaskEntityContext context, TaskEntityState state)
    {
        Name = name;
        _input = input;
        Context = context;
        State = state;
        HasInput = input is not null;
    }

    public override string Name { get; }

    public override TaskEntityContext Context { get; }

    public override TaskEntityState State { get; }

    public override bool HasInput { get; }

    public override object? GetInput(Type inputType)
        => FakeDurableOrchestrationRunner.ConvertValue(_input, inputType);
}
