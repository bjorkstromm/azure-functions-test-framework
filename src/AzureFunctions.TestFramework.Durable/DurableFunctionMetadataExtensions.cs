using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;
using System.Text.Json;

namespace AzureFunctions.TestFramework.Durable;

/// <summary>
/// Helper methods for inspecting Durable Functions bindings in discovered metadata.
/// </summary>
public static class DurableFunctionMetadataExtensions
{
    /// <summary>
    /// Returns <see langword="true"/> when the function metadata includes the specified binding type.
    /// </summary>
    /// <param name="metadata">The function metadata to inspect.</param>
    /// <param name="bindingType">The binding type to search for, such as <c>durableClient</c>.</param>
    /// <returns><see langword="true"/> when the binding type is present; otherwise, <see langword="false"/>.</returns>
    public static bool HasBindingType(this IFunctionMetadata metadata, string bindingType)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingType);

        foreach (var rawBinding in metadata.RawBindings ?? [])
        {
            using var document = JsonDocument.Parse(rawBinding);
            if (!document.RootElement.TryGetProperty("type", out var typeElement))
            {
                continue;
            }

            if (string.Equals(typeElement.GetString(), bindingType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the Durable trigger binding type for a function, if one exists.
    /// </summary>
    /// <param name="metadata">The function metadata to inspect.</param>
    /// <returns>
    /// The trigger type, such as <c>orchestrationTrigger</c>, <c>activityTrigger</c>, or
    /// <c>entityTrigger</c>; otherwise, <see langword="null"/>.
    /// </returns>
    public static string? GetDurableTriggerType(this IFunctionMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        foreach (var bindingType in new[] { "orchestrationTrigger", "activityTrigger", "entityTrigger" })
        {
            if (metadata.HasBindingType(bindingType))
            {
                return bindingType;
            }
        }

        return null;
    }
}
