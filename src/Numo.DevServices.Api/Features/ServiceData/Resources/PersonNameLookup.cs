using Numo.Person.Lib;
using Numo.Person.Lib.Models;

namespace Numo.DevServices.Api.Features.ServiceData.Resources;

/// <summary>What a person search found, and whether the cap cut it short.</summary>
public sealed record PersonIdSearch(IReadOnlyList<Guid> PersonIds, bool IsTruncated);

/// <summary>
/// The two person lookups the Employee service cannot answer itself: it holds no name field at all,
/// so its rows are columns of GUIDs without the first, and it cannot search by name at all without
/// the second. Both live here because the employees and positions resources each need both.
/// </summary>
public sealed class PersonNameLookup(IPersonClient personClient)
{
    /// <summary>
    /// The most person ids one call may carry. BaseClient switches to POST {endpoint}/search above
    /// 1000 characters of query string and this slice is GET-only; a repeated PersonIds GUID costs 46
    /// of those characters, and 20 of them plus the paging and soft-delete parameters measure 989.
    /// </summary>
    public const int MaxIdsPerCall = 20;

    private const int FirstPage = 1;

    /// <summary>The Person service excludes soft-deleted people unless told how far back to include
    /// them, so including them means a date old enough to cover every row rather than a flag.</summary>
    private static readonly DateOnly IncludeDeletedSince = new(1900, 1, 1);

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
                IncludeDeletedSince = IncludeDeletedSince,
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
    /// </summary>
    public async Task<PersonIdSearch> FindPersonIdsByNameAsync(string nameFragment)
    {
        var filter = new PersonFilter
        {
            Page = FirstPage,
            PageSize = MaxIdsPerCall + 1,
            FullNamePart = nameFragment,
        };

        var persons = await DownstreamCall.InvokeAsync(
            () => personClient.GetPersons(filter),
            "a person search by name");

        var personIds = persons.Select(person => person.Id).Distinct().ToList();

        return personIds.Count > MaxIdsPerCall
            ? new PersonIdSearch(personIds.Take(MaxIdsPerCall).ToList(), IsTruncated: true)
            : new PersonIdSearch(personIds, IsTruncated: false);
    }

    private static string? FullName(PersonDto person)
    {
        var name = $"{person.FirstName} {person.LastName}".Trim();
        return string.IsNullOrEmpty(name) ? null : name;
    }
}
