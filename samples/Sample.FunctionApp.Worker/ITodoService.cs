namespace Sample.FunctionApp.Worker;

/// <summary>
/// Provides CRUD operations for <see cref="TodoItem"/> entities.
/// </summary>
public interface ITodoService
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    Task<IEnumerable<TodoItem>> GetAllAsync();
    /// <summary>
    /// Executes this operation.
    /// </summary>
    Task<TodoItem?> GetByIdAsync(string id);
    /// <summary>
    /// Executes this operation.
    /// </summary>
    Task<TodoItem> CreateAsync(TodoItem item);
    /// <summary>
    /// Executes this operation.
    /// </summary>
    Task<TodoItem?> UpdateAsync(string id, TodoItem updates);
    /// <summary>
    /// Executes this operation.
    /// </summary>
    Task<bool> DeleteAsync(string id);
}
