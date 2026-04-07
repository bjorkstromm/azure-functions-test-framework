using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TestProject;

/// <summary>Tests for Services, ConfigureSetting, env vars, and ALC isolation.</summary>
public abstract class FrameworkFeaturesTestsBase : TestHostTestBase
{
    protected FrameworkFeaturesTestsBase(ITestOutputHelper output) : base(output) { }

    protected abstract Task<IFunctionsTestHost> CreateTestHostWithServicesAsync(Action<IServiceCollection> configure);

    // Override CreateTestHostAsync to not be required for individual tests in this class
    protected override Task<IFunctionsTestHost> CreateTestHostAsync() => CreateTestHostWithServicesAsync(_ => { });

    [Fact]
    public async Task Services_ReturnsConfiguredSingletonService()
    {
        var seededItems = new List<Item> { new() { Id = "seed-id", Name = "Seeded Item" } };
        var seededService = new SeededItemService(seededItems);

        await using var testHost = await CreateTestHostWithServicesAsync(
            services => services.AddSingleton<IItemService>(seededService));

        var resolvedService = testHost.Services.GetRequiredService<IItemService>();
        Assert.Same(seededService, resolvedService);
    }

    [Fact]
    public async Task InProcessMethodInfoLocator_PreventsAssemblyDualLoading()
    {
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Where(a => !(a.GetName().Name?.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) ?? false))
            .GroupBy(a => a.GetName().Name)
            .Where(g => g.Count() > 1)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .ToList();

        Assert.Empty(loadedAssemblies);
    }

    private sealed class SeededItemService : IItemService
    {
        private readonly List<Item> _items;
        public SeededItemService(List<Item> items) => _items = items;
        public IReadOnlyList<Item> GetAll() => _items;
        public Item? GetById(string id) => _items.FirstOrDefault(i => i.Id == id);
        public Item Create(string name) { var item = new Item { Id = Guid.NewGuid().ToString(), Name = name }; _items.Add(item); return item; }
        public Item? Update(string id, string name, bool isCompleted)
        {
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item == null) return null;
            item.Name = name; item.IsCompleted = isCompleted;
            return item;
        }
        public bool Delete(string id) { var item = _items.FirstOrDefault(i => i.Id == id); if (item == null) return false; _items.Remove(item); return true; }
    }
}
