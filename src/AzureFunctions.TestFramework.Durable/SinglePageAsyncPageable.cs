using Microsoft.DurableTask;

namespace AzureFunctions.TestFramework.Durable;

internal sealed class SinglePageAsyncPageable<T> : AsyncPageable<T> where T : notnull
{
    private readonly IReadOnlyList<T> _values;

    public SinglePageAsyncPageable(IReadOnlyList<T> values)
    {
        _values = values;
    }

    public override IAsyncEnumerable<Page<T>> AsPages(string? continuationToken = null, int? pageSizeHint = null)
    {
        return GetPages();
    }

    private async IAsyncEnumerable<Page<T>> GetPages()
    {
        yield return new Page<T>(_values, continuationToken: null);
    }
}
