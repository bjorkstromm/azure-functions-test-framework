namespace Sample.FunctionApp.CustomRoutePrefix;

/// <summary>Represents a product in the catalogue.</summary>
public class Product
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

/// <summary>Provides CRUD operations for products.</summary>
public interface IProductService
{
    IReadOnlyList<Product> GetAll();
    Product? GetById(string id);
    Product Create(string name, decimal price);
    bool Delete(string id);
}

/// <summary>In-memory implementation of <see cref="IProductService"/> for testing.</summary>
public class InMemoryProductService : IProductService
{
    private readonly List<Product> _products = new();
    private readonly object _lock = new();

    public IReadOnlyList<Product> GetAll()
    {
        lock (_lock) return _products.ToList();
    }

    public Product? GetById(string id)
    {
        lock (_lock) return _products.FirstOrDefault(p => p.Id == id);
    }

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
