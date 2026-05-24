namespace TestProject;

/// <summary>
/// Defines the contract for this type.
/// </summary>
public interface IProcessedItemsService
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    void Add(string item);
    /// <summary>
    /// Executes this operation.
    /// </summary>
    IReadOnlyList<string> TakeAll();
    /// <summary>
    /// Executes this operation.
    /// </summary>
    void Reset();
}

/// <summary>
/// Represents this type.
/// </summary>
public sealed class InMemoryProcessedItemsService : IProcessedItemsService
{
    private readonly List<string> _items = [];
    private readonly object _lock = new();

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public void Add(string item)
    {
        lock (_lock) _items.Add(item);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public IReadOnlyList<string> TakeAll()
    {
        lock (_lock)
        {
            var result = _items.ToList();
            _items.Clear();
            return result;
        }
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public void Reset()
    {
        lock (_lock) _items.Clear();
    }
}
