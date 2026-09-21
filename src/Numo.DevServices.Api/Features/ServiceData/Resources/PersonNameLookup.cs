using Numo.Person.Lib;
using Numo.Person.Lib.Models;

namespace Numo.DevServices.Api.Features.ServiceData.Resources;

/// <summary>What a person search found, and whether the caller's cap cut it short.</summary>
public sealed record PersonIdSearch(IReadOnlyList<Guid> PersonIds, bool IsTruncated);

/// <summary>
/// The two person lookups the employee-side services cannot answer themselves: they hold no name
/// field at all, so their rows are columns of GUIDs without the first, and they cannot search a name
/// at all without the second. Both live here because the employees and positions resources need both.
///
/// Every call is a GET, which is a real constraint rather than a preference: BaseClient switches to
/// POST {endpoint}/search above 1000 characters of query string, and one of the routes this seam
/// feeds has no search variant. The two numbers below are therefore query-string budgets, and they
/// are deliberately separate because they are spent in different requests.
/// </summary>
public sealed class PersonNameLookup(IPersonClient personClient)
{
    /// <summary>
    /// How many ids one id-to-name call may carry. Budget: a persons GET carrying nothing but
    /// PersonIds, paging and IncludeDeletedSince, measured at 989 characters for 20 ids. Private
    /// because it says nothing about any other request; a repeated PersonIds GUID costs 47 characters
    /// wherever it is spent, but what else shares the query string is the caller's business.
    /// </summary>
    private const int MaxIdsPerCall = 20;

    private const int FirstPage = 1;

    /// <summary>
    /// Display names for a whole page of rows: one call per <see cref="MaxIdsPerCall"/> distinct ids
    /// rather than one per row. Soft-deleted people are included because this resolves a label and
    /// can never add a row, and an employee of a deleted person would otherwise be nameless. An id
    /// the service returns nothing for, and a person with no name at all, are both simply absent from
    /// the result, so a caller renders an empty cell rather than inventing text.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesByPersonIdAsync(IEnumerable<Guid> personIds)
    {
        var namesByPersonId = new Dictionary<Guid, string>();

        foreach (var chunk in personIds.Where(id => id != Guid.Empty).Distinct().Chunk(MaxIdsPerCall))
        {
            // Page and PageSize are always set: an unset filter is a full-table read.
            var filter = new PersonFilter
            {
                Page = FirstPage,
                PageSize = chunk.Length,
                PersonIds = chunk,
                IncludeDeletedSince = ResourceQueryFilters.IncludeDeletedSince,
            };

            var persons = await DownstreamCall.InvokeAsync(
                () => personClient.GetPersons(filter),
                $"the names of {chunk.Length} people");

            foreach (var person in persons)
            {
                var name = FullName(person);

                if (name is not null)
                {
                    namesByPersonId[person.Id] = name;
                }
            }
        }

        return namesByPersonId;
    }

    /// <summary>
    /// The pre-search behind the personName filter of the employee-side resources: those services
    /// cannot search a name, so a name fragment is resolved to person ids here and the ids feed the
    /// resource's own PersonIds filter. One id more than the cap is asked for, so truncation is a
    /// fact the caller can report rather than a guess.
    ///
    /// Soft-deleted people are included, matching <see cref="GetNamesByPersonIdAsync"/>: whether a
    /// person is deleted says nothing about whether the rows referencing them are, and the calling
    /// resource has its own includeDeleted filter for that question.
    /// </summary>
    /// <param name="narrowToPersonId">A person the caller already restricts to. Passed downstream
    /// alongside the name so the two narrow each other exactly, instead of the caller intersecting a
    /// list the cap may already have truncated.</param>
    /// <param name="maxIds">How many ids the caller can afford to spend. There is no default: the ids
    /// are spent inside a different service's filter, whose other parameters only the caller knows,
    /// and a shared number would silently overflow the widest of them.</param>
    public async Task<PersonIdSearch> FindPersonIdsByNameAsync(
        string nameFragment,
        Guid? narrowToPersonId,
        int maxIds)
    {
        var filter = new PersonFilter
        {
            Page = FirstPage,
            PageSize = maxIds + 1,
            FullNamePart = nameFragment,
            PersonIds = narrowToPersonId is null ? [] : [narrowToPersonId.Value],
            IncludeDeletedSince = ResourceQueryFilters.IncludeDeletedSince,
        };

        var persons = await DownstreamCall.InvokeAsync(
            () => personClient.GetPersons(filter),
            "a person search by name");

        var personIds = persons.Select(person => person.Id).Distinct().ToList();

        return personIds.Count > maxIds
            ? new PersonIdSearch(personIds.Take(maxIds).ToList(), IsTruncated: true)
            : new PersonIdSearch(personIds, IsTruncated: false);
    }

    private static string? FullName(PersonDto person)
    {
        var name = $"{person.FirstName} {person.LastName}".Trim();
        return string.IsNullOrEmpty(name) ? null : name;
    }
}
