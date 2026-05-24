namespace Sample.FunctionApp.CustomRoutePrefix;

/// <summary>Represents a product in the catalogue.</summary>
public class Product
{
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public decimal Price { get; set; }
}

/// <summary>Provides CRUD operations for products.</summary>
public interface IProductService
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    IReadOnlyList<Product> GetAll();
    /// <summary>
    /// Executes this operation.
    /// </summary>
    Product? GetById(string id);
    /// <summary>
    /// Executes this operation.
    /// </summary>
    Product Create(string name, decimal price);
    /// <summary>
    /// Executes this operation.
    /// </summary>
    bool Delete(string id);
}

/// <summary>In-memory implementation of <see cref="IProductService"/> for testing.</summary>
public class InMemoryProductService : IProductService
{
    private readonly List<Product> _products = new();
    private readonly object _lock = new();

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public IReadOnlyList<Product> GetAll()
    {
        lock (_lock) return _products.ToList();
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public Product? GetById(string id)
    {
        lock (_lock) return _products.FirstOrDefault(p => p.Id == id);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public Product Create(string name, decimal price)
    {
        var product = new Product
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Price = price
        };
        lock (_lock) _products.Add(product);
        return product;
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public bool Delete(string id)
    {
        lock (_lock)
        {
            var product = _products.FirstOrDefault(p => p.Id == id);
            if (product == null) return false;
            _products.Remove(product);
            return true;
        }
    }

    /// <summary>Resets the store — call between tests to guarantee isolation.</summary>
    public void Reset()
    {
        lock (_lock) _products.Clear();
    }
}
