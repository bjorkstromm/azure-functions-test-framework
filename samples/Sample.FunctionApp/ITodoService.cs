namespace Sample.FunctionApp;

/// <summary>
/// Provides CRUD operations for <see cref="TodoItem"/> entities.
/// Register an implementation in DI so that <see cref="TodoFunctions"/> can be injected and tested.
/// </summary>
public interface ITodoService
{
    Task<IEnumerable<TodoItem>> GetAllAsync();
    Task<TodoItem?> GetByIdAsync(string id);
    Task<TodoItem> CreateAsync(TodoItem item);
    Task<TodoItem?> UpdateAsync(string id, TodoItem updates);
    Task<bool> DeleteAsync(string id);
}
