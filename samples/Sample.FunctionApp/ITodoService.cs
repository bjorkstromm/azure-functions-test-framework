namespace Sample.FunctionApp;

public interface ITodoService
{
    IReadOnlyList<TodoItem> GetAll();
    TodoItem? GetById(string id);
    TodoItem Create(string title);
    bool Delete(string id);
}

public sealed class TodoItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}
