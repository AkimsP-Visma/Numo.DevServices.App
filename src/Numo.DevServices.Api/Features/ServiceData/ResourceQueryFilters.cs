namespace Numo.DevServices.Api.Features.ServiceData;

/// <summary>
/// Reading a declared filter out of a <see cref="ResourceQuery"/>, in one place because every
/// resource reads its filters the same way and depends on nothing but the query to do it. A value
/// that does not parse for the kind the resource declared is treated as absent: the page validator
/// has already rejected a malformed one, so reaching here means the resource asked for a filter it
/// did not declare.
/// </summary>
internal static class ResourceQueryFilters
{
    /// <summary>
    /// What "include soft-deleted rows" means downstream. Neither service takes a flag for it -
    /// IncludeDeletedSince is a date - so including them means a date old enough to cover every row.
    /// </summary>
    public static readonly DateOnly IncludeDeletedSince = new(1900, 1, 1);

    public static string? ReadText(ResourceQuery query, string key)
        => query.Filters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    public static bool? ReadBoolean(ResourceQuery query, string key)
        => query.Filters.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : null;

    public static Guid? ReadGuid(ResourceQuery query, string key)
        => query.Filters.TryGetValue(key, out var value) && Guid.TryParse(value, out var parsed) ? parsed : null;

    /// <summary>The date when the caller asked for soft-deleted rows, and null when it did not, which
    /// is what leaves them excluded.</summary>
    public static DateOnly? ReadIncludeDeletedSince(ResourceQuery query, string key)
        => ReadBoolean(query, key) is true ? IncludeDeletedSince : null;
}
