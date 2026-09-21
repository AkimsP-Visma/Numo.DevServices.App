using System.Globalization;

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

    /// <summary>
    /// How a list-valued filter carries its values in one query-string value, chosen here so no
    /// resource invents an encoding of its own.
    /// </summary>
    public const char ListDelimiter = ',';

    /// <summary>
    /// How many values a list-valued filter may carry, enforced by the page validator so an overlong
    /// list is a 400 rather than a downstream surprise. The number is the narrowest downstream
    /// query-string budget in the slice: the client library switches to POST {endpoint}/search above
    /// 1000 characters, and <c>/api/positions/view/search</c> does not exist (probed: 404), so an
    /// overflow there is a hard failure. Four is what that budget affords once the person pre-search
    /// has been paid for: see PositionsResource.MaxPreSearchIds for the arithmetic the two share.
    /// </summary>
    public const int MaxListValues = 4;

    public static string? ReadText(ResourceQuery query, string key)
        => query.Filters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    public static bool? ReadBoolean(ResourceQuery query, string key)
        => query.Filters.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : null;

    public static Guid? ReadGuid(ResourceQuery query, string key)
        => query.Filters.TryGetValue(key, out var value) && Guid.TryParse(value, out var parsed) ? parsed : null;

    public static DateOnly? ReadDate(ResourceQuery query, string key)
        => query.Filters.TryGetValue(key, out var value)
            && DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;

    /// <summary>
    /// How a list-valued filter's value divides into values, used by both the page validator and the
    /// readers below so that the elements a cap is counted against are the elements a resource
    /// actually sends. A trailing delimiter is therefore not a failure, and an empty element is not
    /// counted.
    /// </summary>
    public static string[] SplitList(string value)
        => value.Split(ListDelimiter, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// The values of a list-valued filter. Null rather than an empty list when the caller asked for
    /// nothing, because the downstream services ignore an empty id array and would answer a request
    /// carrying one with the whole table. The page validator rejects an unusable value before this
    /// runs, including an all-zero GUID, so that a discarded value can never reach a service as a
    /// missing filter.
    /// </summary>
    public static IReadOnlyList<Guid>? ReadGuids(ResourceQuery query, string key)
    {
        if (!query.Filters.TryGetValue(key, out var value))
        {
            return null;
        }

        var ids = SplitList(value)
            .Select(part => Guid.TryParse(part, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        return ids.Count == 0 ? null : ids;
    }

    /// <summary>The date when the caller asked for soft-deleted rows, and null when it did not, which
    /// is what leaves them excluded.</summary>
    public static DateOnly? ReadIncludeDeletedSince(ResourceQuery query, string key)
        => ReadBoolean(query, key) is true ? IncludeDeletedSince : null;
}
