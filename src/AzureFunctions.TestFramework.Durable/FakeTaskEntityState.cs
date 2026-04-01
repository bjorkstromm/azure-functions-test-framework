using System.Text.Json;
using Microsoft.DurableTask.Entities;

namespace AzureFunctions.TestFramework.Durable;

internal sealed class FakeTaskEntityState : TaskEntityState
{
    private string? _serializedState;

    public FakeTaskEntityState(string? serializedState = null)
    {
        _serializedState = serializedState;
    }

    public override bool HasState => _serializedState is not null;

    public string? SerializedState => _serializedState;

    public override object? GetState(Type type)
    {
        if (_serializedState is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize(_serializedState, type);
    }

    public override void SetState(object? state)
    {
        _serializedState = state is null ? null : JsonSerializer.Serialize(state);
    }
}
