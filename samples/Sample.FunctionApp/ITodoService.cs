namespace Sample.FunctionApp;

/// <summary>
/// Defines the contract for this type.
/// </summary>
public interface ITodoService
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    IReadOnlyList<TodoItem> GetAll();
    /// <summary>
    /// Executes this operation.
    /// </summary>
    TodoItem? GetById(string id);
    /// <summary>
    /// Executes this operation.
    /// </summary>
    TodoItem Create(string title);
    /// <summary>
    /// Executes this operation.
    /// </summary>
    bool Delete(string id);
}

/// <summary>
/// Represents this type.
/// </summary>
public sealed class TodoItem
{
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public bool IsCompleted { get; set; }
}
