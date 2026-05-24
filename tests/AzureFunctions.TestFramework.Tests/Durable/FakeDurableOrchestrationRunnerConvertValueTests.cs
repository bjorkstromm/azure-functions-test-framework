using AzureFunctions.TestFramework.Durable;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Durable;

/// <summary>
/// Unit tests for <see cref="FakeDurableOrchestrationRunner.ConvertValue"/>.
/// </summary>
public class FakeDurableOrchestrationRunnerConvertValueTests
{
    [Fact]
    public void ConvertValue_Null_ReferenceType_ReturnsNull()
    {
        var result = FakeDurableOrchestrationRunner.ConvertValue(null, typeof(string));
        Assert.Null(result);
    }

    [Fact]
    public void ConvertValue_Null_NullableValueType_ReturnsNull()
    {
        var result = FakeDurableOrchestrationRunner.ConvertValue(null, typeof(int?));
        Assert.Null(result);
    }

    [Fact]
    public void ConvertValue_Null_NonNullableValueType_ReturnsDefault()
    {
        var result = FakeDurableOrchestrationRunner.ConvertValue(null, typeof(int));
        Assert.Equal(0, result);
    }

    [Fact]
    public void ConvertValue_Null_BoolValueType_ReturnsFalse()
    {
        var result = FakeDurableOrchestrationRunner.ConvertValue(null, typeof(bool));
        Assert.Equal(false, result);
    }

    [Fact]
    public void ConvertValue_AlreadyCorrectType_ReturnsSameInstance()
    {
        var value = "hello";
        var result = FakeDurableOrchestrationRunner.ConvertValue(value, typeof(string));
        Assert.Same(value, result);
    }

    [Fact]
    public void ConvertValue_DerivedType_ReturnsSameInstance()
    {
        var value = new DerivedDto { Name = "test", Extra = 42 };
        var result = FakeDurableOrchestrationRunner.ConvertValue(value, typeof(BaseDto));
        Assert.Same(value, result);
    }

    [Fact]
    public void ConvertValue_AnonymousObject_ConvertsToTargetType()
    {
        var value = new { Name = "Alice", Age = 30 };
        var result = FakeDurableOrchestrationRunner.ConvertValue(value, typeof(PersonDto));
        var person = Assert.IsType<PersonDto>(result);
        Assert.Equal("Alice", person.Name);
        Assert.Equal(30, person.Age);
    }

    [Fact]
    public void ConvertValue_IntToLong_Deserializes()
    {
        // int is not assignable to long directly, so goes through JSON round-trip
        var value = 42;
        var result = FakeDurableOrchestrationRunner.ConvertValue(value, typeof(long));
        Assert.Equal(42L, result);
    }

    [Fact]
    public void ConvertValue_DictionaryToDto_Deserializes()
    {
        var value = new Dictionary<string, object?> { ["Name"] = "Bob", ["Age"] = 25 };
        var result = FakeDurableOrchestrationRunner.ConvertValue(value, typeof(PersonDto));
        var person = Assert.IsType<PersonDto>(result);
        Assert.Equal("Bob", person.Name);
    }

    private class BaseDto { public string? Name { get; set; } }
    private sealed class DerivedDto : BaseDto { public int Extra { get; set; } }
    private sealed class PersonDto { public string? Name { get; set; } public int Age { get; set; } }
}
