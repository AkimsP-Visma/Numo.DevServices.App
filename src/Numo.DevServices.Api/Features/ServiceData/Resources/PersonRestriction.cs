namespace Numo.DevServices.Api.Features.ServiceData.Resources;

/// <summary>
/// Which people a page of an employee-side resource is restricted to. Shared because the rule below
/// is enforced by no type: a resource that hand-copies it and drops <see cref="MatchesNobody"/>
/// reads a whole table instead of nothing, and nothing about that fails to compile.
///
/// Only the three states live here. How a resource arrives at them stays with the resource, because
/// each narrows by different filters of its own.
/// </summary>
/// <param name="PersonIds">Null when the query restricts nothing. An empty list is not the same
/// thing: an empty PersonIds array is ignored downstream, so it would read the whole table instead of
/// nothing, which is what <see cref="MatchesNobody"/> exists to prevent.</param>
/// <param name="Notice">What the resource had to truncate to arrive at these ids, if anything.</param>
internal sealed record PersonRestriction(IReadOnlyList<Guid>? PersonIds, string? Notice)
{
    public static readonly PersonRestriction None = new(PersonIds: null, Notice: null);

    /// <summary>True when the resource must skip the downstream call entirely rather than send an
    /// empty id array.</summary>
    public bool MatchesNobody => PersonIds is { Count: 0 };
}
