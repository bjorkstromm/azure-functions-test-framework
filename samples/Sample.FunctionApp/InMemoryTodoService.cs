namespace Sample.FunctionApp;

/// <summary>
/// Represents this type.
/// </summary>
public sealed class InMemoryTodoService : ITodoService
{
    private readonly List<TodoItem> _todos = [];
    private readonly object _lock = new();

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public IReadOnlyList<TodoItem> GetAll() { lock (_lock) return _todos.ToList(); }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public TodoItem? GetById(string id) { lock (_lock) return _todos.FirstOrDefault(t => t.Id == id); }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public TodoItem Create(string title)
    {
        var todo = new TodoItem { Id = Guid.NewGuid().ToString(), Title = title };
        lock (_lock) _todos.Add(todo);
        return todo;
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public bool Delete(string id)
    {
        lock (_lock)
        {
            var todo = _todos.FirstOrDefault(t => t.Id == id);
            if (todo == null) return false;
            _todos.Remove(todo);
            return true;
        }
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public void Reset() { lock (_lock) _todos.Clear(); }
}
