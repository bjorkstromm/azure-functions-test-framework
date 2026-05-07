using Microsoft.DurableTask;

namespace AzureFunctions.TestFramework.Durable;

internal sealed class SinglePageAsyncPageable<T> : AsyncPageable<T> where T : notnull
{
    private readonly IReadOnlyList<T> _values;

    public SinglePageAsyncPageable(IReadOnlyList<T> values)
    {
        _values = values;
    }

    public override async IAsyncEnumerable<Page<T>> AsPages(string? continuationToken = null, int? pageSizeHint = null)
    {
        yield return new Page<T>(_values, continuationToken: null);
    }
}
