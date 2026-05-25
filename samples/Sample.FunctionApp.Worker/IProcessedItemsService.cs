namespace Sample.FunctionApp.Worker;

/// <summary>
/// A simple in-memory service for tracking items processed by trigger functions.
/// Used in integration tests to assert that triggered functions ran and received the expected data.
/// </summary>
public interface IProcessedItemsService
{
    /// <summary>Records a processed item.</summary>
    void Add(string item);

    /// <summary>Returns all recorded items and clears the list.</summary>
    IReadOnlyList<string> TakeAll();

    /// <summary>Clears all recorded items.</summary>
    void Reset();
}

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IProcessedItemsService"/>.
/// </summary>
public sealed class InMemoryProcessedItemsService : IProcessedItemsService
{
    private readonly List<string> _items = new();
    private readonly object _lock = new();

    public void Add(string item)
    {
        lock (_lock)
        {
            _items.Add(item);
        }
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
        lock (_lock)
        {
            _items.Clear();
        }
    }
}
