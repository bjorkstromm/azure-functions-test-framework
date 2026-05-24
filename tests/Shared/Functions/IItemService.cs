namespace TestProject;

/// <summary>
/// Defines the contract for this type.
/// </summary>
public interface IItemService
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    IReadOnlyList<Item> GetAll();
    /// <summary>
    /// Executes this operation.
    /// </summary>
    Item? GetById(string id);
    /// <summary>
    /// Executes this operation.
    /// </summary>
    Item Create(string name);
    /// <summary>
    /// Executes this operation.
    /// </summary>
    Item? Update(string id, string name, bool isCompleted);
    /// <summary>
    /// Executes this operation.
    /// </summary>
    bool Delete(string id);
}

/// <summary>
/// Represents this type.
/// </summary>
public sealed class Item
{
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public bool IsCompleted { get; set; }
}
