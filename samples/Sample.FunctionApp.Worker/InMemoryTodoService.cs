namespace Sample.FunctionApp.Worker;

/// <summary>
/// An in-memory implementation of <see cref="ITodoService"/> suitable for development and testing.
/// </summary>
public sealed class InMemoryTodoService : ITodoService
{
    private readonly List<TodoItem> _todos = new();

    /// <summary>Removes all todos, returning the service to a clean state for the next test.</summary>
    public void Reset() => _todos.Clear();

    public Task<IEnumerable<TodoItem>> GetAllAsync()
        => Task.FromResult<IEnumerable<TodoItem>>(_todos);

    public Task<TodoItem?> GetByIdAsync(string id)
        => Task.FromResult(_todos.FirstOrDefault(t => t.Id == id));

    public Task<TodoItem> CreateAsync(TodoItem item)
    {
        item.Id = Guid.NewGuid().ToString();
        item.CreatedAt = DateTime.UtcNow;
        _todos.Add(item);
        return Task.FromResult(item);
    }

    public Task<TodoItem?> UpdateAsync(string id, TodoItem updates)
    {
        var existing = _todos.FirstOrDefault(t => t.Id == id);
        if (existing == null) return Task.FromResult<TodoItem?>(null);

        existing.Title = updates.Title;
        existing.IsCompleted = updates.IsCompleted;
        existing.UpdatedAt = DateTime.UtcNow;
        return Task.FromResult<TodoItem?>(existing);
    }

    public Task<bool> DeleteAsync(string id)
    {
        var todo = _todos.FirstOrDefault(t => t.Id == id);
        if (todo == null) return Task.FromResult(false);
        _todos.Remove(todo);
        return Task.FromResult(true);
    }
}
