using AzureFunctions.TestFramework.Durable;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Durable;

/// <summary>
/// Unit tests for <see cref="FakeTaskEntityState"/>, verifying state initialisation,
/// serialisation round-trips, and the <see cref="FakeTaskEntityState.HasState"/> flag.
/// </summary>
public class FakeTaskEntityStateTests
{
    // ── HasState ──────────────────────────────────────────────────────────────

    [Fact]
    public void HasState_InitiallyFalse()
    {
        var state = new FakeTaskEntityState();
        Assert.False(state.HasState);
    }

    [Fact]
    public void HasState_AfterSetState_NonNull_ReturnsTrue()
    {
        var state = new FakeTaskEntityState();
        state.SetState(42);
        Assert.True(state.HasState);
    }

    [Fact]
    public void HasState_AfterSetState_Null_ReturnsFalse()
    {
        var state = new FakeTaskEntityState();
        state.SetState(42);
        state.SetState(null);
        Assert.False(state.HasState);
    }

    [Fact]
    public void HasState_AfterSetState_EmptyString_ReturnsTrue()
    {
        // An empty string is a valid (non-null) value — HasState should be true.
        var state = new FakeTaskEntityState();
        state.SetState(string.Empty);
        Assert.True(state.HasState);
    }

    // ── Constructor with initial serialized state ─────────────────────────────

    [Fact]
    public void Constructor_WithNullSerializedState_HasState_False()
    {
        var state = new FakeTaskEntityState(null);
        Assert.False(state.HasState);
    }

    [Fact]
    public void Constructor_WithSerializedState_HasState_True()
    {
        var state = new FakeTaskEntityState("42");
        Assert.True(state.HasState);
    }

    // ── GetState ──────────────────────────────────────────────────────────────

    [Fact]
    public void GetState_BeforeSetState_ReturnsNull()
    {
        var state = new FakeTaskEntityState();
        Assert.Null(state.GetState(typeof(string)));
    }

    [Fact]
    public void GetState_AfterSetState_Int_ReturnsDeserializedValue()
    {
        var state = new FakeTaskEntityState();
        state.SetState(99);
        var result = state.GetState(typeof(int));
        Assert.Equal(99, result);
    }

    [Fact]
    public void GetState_AfterSetState_String_ReturnsDeserializedValue()
    {
        var state = new FakeTaskEntityState();
        state.SetState("hello");
        var result = state.GetState(typeof(string));
        Assert.Equal("hello", result);
    }

    [Fact]
    public void GetState_AfterSetState_ComplexType_RoundTrips()
    {
        var state = new FakeTaskEntityState();
        state.SetState(new CounterState(Name: "my-counter", Value: 7));

        var result = state.GetState(typeof(CounterState)) as CounterState;

        Assert.NotNull(result);
        Assert.Equal("my-counter", result!.Name);
        Assert.Equal(7, result.Value);
    }

    // ── SerializedState property ──────────────────────────────────────────────

    [Fact]
    public void SerializedState_InitiallyNull()
    {
        var state = new FakeTaskEntityState();
        Assert.Null(state.SerializedState);
    }

    [Fact]
    public void SerializedState_AfterSetState_IsNotNull()
    {
        var state = new FakeTaskEntityState();
        state.SetState(42);
        Assert.NotNull(state.SerializedState);
    }

    [Fact]
    public void SerializedState_AfterSetState_Null_IsNull()
    {
        var state = new FakeTaskEntityState();
        state.SetState(42);
        state.SetState(null);
        Assert.Null(state.SerializedState);
    }

    [Fact]
    public void SerializedState_ConstructorValue_IsPreserved()
    {
        const string raw = "42";
        var state = new FakeTaskEntityState(raw);
        Assert.Equal(raw, state.SerializedState);
    }

    // ── Round-trip consistency ────────────────────────────────────────────────

    [Fact]
    public void SetState_ThenGetState_ProducesOriginalValue()
    {
        var state = new FakeTaskEntityState();
        state.SetState(3.14);
        var result = state.GetState(typeof(double));
        Assert.Equal(3.14, result);
    }

    [Fact]
    public void SetState_MultipleUpdates_LastValueWins()
    {
        var state = new FakeTaskEntityState();
        state.SetState(1);
        state.SetState(2);
        state.SetState(3);
        var result = state.GetState(typeof(int));
        Assert.Equal(3, result);
    }

    // ── Test helper ──────────────────────────────────────────────────────────

    private sealed record CounterState(string Name, int Value);
}
