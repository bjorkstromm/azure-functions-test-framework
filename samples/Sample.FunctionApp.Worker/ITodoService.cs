namespace Sample.FunctionApp.Worker;

/// <summary>
/// Provides CRUD operations for <see cref="TodoItem"/> entities.
/// </summary>
public interface ITodoService
{
    Task<IEnumerable<TodoItem>> GetAllAsync();
    Task<TodoItem?> GetByIdAsync(string id);
    Task<TodoItem> CreateAsync(TodoItem item);
    Task<TodoItem?> UpdateAsync(string id, TodoItem updates);
    Task<bool> DeleteAsync(string id);
}
