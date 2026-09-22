namespace Numo.DevServices.Api.Features.ServiceData;

/// <summary>
/// The four paging rules every resource obeys, in one place so no resource can get them wrong: the
/// page index and the page size are always passed to the fetch, the page size is passed unchanged,
/// HasMore comes from fetching page N+1 at that same page size, and a page that came back short is
/// already the last one so the peek is skipped. A resource supplies only how to fetch a page and how
/// to turn its items into rows.
/// </summary>
internal static class ResourcePageBuilder
{
    /// <param name="fetchPageAsync">Fetches one page, given the page index and the page size to ask
    /// for. Both are supplied rather than read off the query so that using anything else is a visible
    /// act.</param>
    /// <param name="toRowsAsync">Projects the fetched page as a whole, which is what a resource
    /// needing one batched lookup per page for its row labels requires. It never runs on the peek
    /// page, so the peek costs one call and not a projection.</param>
    /// <param name="notice">What the resource had to truncate, if anything.</param>
    public static async Task<ResourcePage> BuildAsync<TItem>(
        ResourceQuery query,
        Func<int, int, Task<IEnumerable<TItem>>> fetchPageAsync,
        Func<IReadOnlyList<TItem>, Task<IReadOnlyList<ResourceRow>>> toRowsAsync,
        string? notice = null)
    {
        IReadOnlyList<TItem> items = (await fetchPageAsync(query.Page, query.PageSize)).ToList();
        var rows = await toRowsAsync(items);

        var hasMore = items.Count == query.PageSize
            && (await fetchPageAsync(query.Page + 1, query.PageSize)).Any();

        return new ResourcePage(rows, query.Page, query.PageSize, hasMore, notice);
    }

    /// <summary>For a resource whose rows need nothing but the fetched item.</summary>
    public static Task<ResourcePage> BuildAsync<TItem>(
        ResourceQuery query,
        Func<int, int, Task<IEnumerable<TItem>>> fetchPageAsync,
        Func<TItem, ResourceRow> toRow,
        string? notice = null)
        => BuildAsync(
            query,
            fetchPageAsync,
            items => Task.FromResult<IReadOnlyList<ResourceRow>>(items.Select(toRow).ToList()),
            notice);

    /// <summary>
    /// For a source with no paging parameters at all - every DataIntegration resource. The whole
    /// (already filtered and sorted) set is fetched once and sliced in memory, which is only honest
    /// because these sets are configuration-sized: a handful of clients, pipelines and connectors,
    /// not the 1300+ rows that made a downstream fetch-per-page the only correct answer for Person
    /// and Employee data. <paramref name="fetchAllAsync"/> must apply the query's own filters, since
    /// nothing here can - by the time a list reaches this method there is no query left to send.
    /// </summary>
    public static async Task<ResourcePage> BuildUnpagedAsync<TItem>(
        ResourceQuery query,
        Func<Task<IEnumerable<TItem>>> fetchAllAsync,
        Func<TItem, ResourceRow> toRow,
        string? notice = null)
    {
        var all = (await fetchAllAsync()).ToList();
        var pageItems = all.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        var hasMore = query.Page * query.PageSize < all.Count;

        return new ResourcePage(pageItems.Select(toRow).ToList(), query.Page, query.PageSize, hasMore, notice);
    }
}
