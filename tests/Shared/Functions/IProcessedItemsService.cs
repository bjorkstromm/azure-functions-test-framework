namespace TestProject;

public interface IProcessedItemsService
{
    void Add(string item);
    IReadOnlyList<string> TakeAll();
    void Reset();
}

public sealed class InMemoryProcessedItemsService : IProcessedItemsService
{
    private readonly List<string> _items = [];
    private readonly object _lock = new();

    public void Add(string item)
    {
        lock (_lock) _items.Add(item);
    }

    public IReadOnlyList<string> TakeAll()
    {
        lock (_lock)
        {
            var result = _items.ToList();
            _items.Clear();
            return result;
        }
    }

    public void Reset()
    {
        lock (_lock) _items.Clear();
    }
}
