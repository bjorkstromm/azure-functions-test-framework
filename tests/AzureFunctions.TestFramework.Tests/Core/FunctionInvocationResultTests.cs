using AzureFunctions.TestFramework.Core;
using System.Text.Json;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Core;

/// <summary>
/// Unit tests for <see cref="FunctionInvocationResult"/>.
/// </summary>
public class FunctionInvocationResultTests
{
    // ── ReadReturnValueAs<T> ──────────────────────────────────────────────────

    [Fact]
    public void ReadReturnValueAs_NullReturnValue_ReturnsDefault()
    {
        var result = new FunctionInvocationResult { ReturnValue = null };
        var value = result.ReadReturnValueAs<string>();
        Assert.Null(value);
    }

    [Fact]
    public void ReadReturnValueAs_TypedValue_ReturnsSameInstance()
    {
        var expected = "hello";
        var result = new FunctionInvocationResult { ReturnValue = expected };
        var value = result.ReadReturnValueAs<string>();
        Assert.Equal(expected, value);
    }

    [Fact]
    public void ReadReturnValueAs_JsonElementToString_Deserializes()
    {
        var json = """{"Name":"Alice"}""";
        var element = JsonSerializer.Deserialize<JsonElement>(json);
        var result = new FunctionInvocationResult { ReturnValue = element };

        var obj = result.ReadReturnValueAs<NameRecord>();

        Assert.NotNull(obj);
        Assert.Equal("Alice", obj!.Name);
    }

    [Fact]
    public void ReadReturnValueAs_IntReturnValue_Deserializes()
    {
        var result = new FunctionInvocationResult { ReturnValue = 42L };
        var value = result.ReadReturnValueAs<long>();
        Assert.Equal(42L, value);
    }

    // ── ReadOutputAs<T> ───────────────────────────────────────────────────────

    [Fact]
    public void ReadOutputAs_ExistingBinding_ReturnsValue()
    {
        var result = new FunctionInvocationResult();
        result.OutputData["myQueue"] = "queue-message";

        var value = result.ReadOutputAs<string>("myQueue");
        Assert.Equal("queue-message", value);
    }

    [Fact]
    public void ReadOutputAs_MissingBinding_ThrowsKeyNotFoundException()
    {
        var result = new FunctionInvocationResult();
        Assert.Throws<KeyNotFoundException>(() => result.ReadOutputAs<string>("nonexistent"));
    }

    [Fact]
    public void ReadOutputAs_NullValue_ReturnsDefault()
    {
        var result = new FunctionInvocationResult();
        result.OutputData["myBinding"] = null;
        var value = result.ReadOutputAs<string>("myBinding");
        Assert.Null(value);
    }

    [Fact]
    public void ReadOutputAs_JsonElementValue_Deserializes()
    {
        var json = """{"Name":"Bob"}""";
        var element = JsonSerializer.Deserialize<JsonElement>(json);
        var result = new FunctionInvocationResult();
        result.OutputData["myBinding"] = element;

        var obj = result.ReadOutputAs<NameRecord>("myBinding");

        Assert.NotNull(obj);
        Assert.Equal("Bob", obj!.Name);
    }

    [Fact]
    public void ReadOutputAs_EmptyBindingName_Throws()
    {
        var result = new FunctionInvocationResult();
        Assert.Throws<ArgumentException>(() => result.ReadOutputAs<string>(""));
    }

    [Fact]
    public void ReadOutputAs_WhitespaceBindingName_Throws()
    {
        var result = new FunctionInvocationResult();
        Assert.Throws<ArgumentException>(() => result.ReadOutputAs<string>("   "));
    }

    // ── Properties ───────────────────────────────────────────────────────────

    [Fact]
    public void DefaultInstance_HasExpectedDefaults()
    {
        var result = new FunctionInvocationResult();

        Assert.Equal(string.Empty, result.InvocationId);
        Assert.False(result.Success);
        Assert.Empty(result.OutputData);
        Assert.Null(result.ReturnValue);
        Assert.Null(result.Error);
        Assert.Empty(result.Logs);
    }

    private sealed record NameRecord(string Name);
}
