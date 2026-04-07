namespace TestProject;

public interface IItemService
{
    IReadOnlyList<Item> GetAll();
    Item? GetById(string id);
    Item Create(string name);
    Item? Update(string id, string name, bool isCompleted);
    bool Delete(string id);
}

public sealed class Item
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}
