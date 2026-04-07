namespace Sample.FunctionApp;

public sealed class InMemoryTodoService : ITodoService
{
    private readonly List<TodoItem> _todos = [];
    private readonly object _lock = new();

    public IReadOnlyList<TodoItem> GetAll() { lock (_lock) return _todos.ToList(); }

    public TodoItem? GetById(string id) { lock (_lock) return _todos.FirstOrDefault(t => t.Id == id); }

    public TodoItem Create(string title)
    {
        var todo = new TodoItem { Id = Guid.NewGuid().ToString(), Title = title };
        lock (_lock) _todos.Add(todo);
        return todo;
    }

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

    public void Reset() { lock (_lock) _todos.Clear(); }
}
