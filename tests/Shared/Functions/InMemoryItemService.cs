namespace TestProject;

public sealed class InMemoryItemService : IItemService
{
    private readonly List<Item> _items = [];
    private readonly object _lock = new();

    public IReadOnlyList<Item> GetAll()
    {
        lock (_lock) return _items.ToList();
    }

    public Item? GetById(string id)
    {
        lock (_lock) return _items.FirstOrDefault(i => i.Id == id);
    }

    public Item Create(string name)
    {
        var item = new Item { Id = Guid.NewGuid().ToString(), Name = name };
        lock (_lock) _items.Add(item);
        return item;
    }

    public Item? Update(string id, string name, bool isCompleted)
    {
        lock (_lock)
        {
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item == null) return null;
            item.Name = name;
            item.IsCompleted = isCompleted;
            return item;
        }
    }

    public bool Delete(string id)
    {
        lock (_lock)
        {
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item == null) return false;
            _items.Remove(item);
            return true;
        }
    }

    public void Reset()
    {
        lock (_lock) _items.Clear();
    }
}
