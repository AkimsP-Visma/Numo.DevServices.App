using System.ComponentModel;
using System.Globalization;
using Numo.Employee.Common.QuerySupport.Ordering;
using Numo.Employee.Lib.Clients.Employee;
using Numo.Employee.Lib.Models;

namespace Numo.DevServices.Api.Features.ServiceData.Resources;

/// <summary>
/// The Employee service's employees. The resource that forces a cross-service call: EmployeeDto has
/// no name field, so every row and every record resolves its person through
/// <see cref="PersonNameLookup"/> - batched once per page, never once per row. The Employee client
/// reports failure by returning a FluentResults result rather than throwing, which
/// <see cref="DownstreamCall.InvokeResultAsync"/> turns into the slice's own error so an empty page
/// can never stand in for a failed call.
/// </summary>
public sealed class EmployeesResource(IEmployeeClient employeeClient, PersonNameLookup personNameLookup)
    : IServiceDataResource
{
    private const string ResourceKey = "employees";
    private const string PersonsResourceKey = "persons";
    private const string PersonNameFilterKey = "personName";
    private const string PersonIdFilterKey = "personId";
    private const string IncludeDeletedFilterKey = "includeDeleted";

    /// <summary>The Employee service excludes soft-deleted rows unless told how far back to include
    /// them, so including them means a date old enough to cover every row rather than a flag.</summary>
    private static readonly DateOnly IncludeDeletedSince = new(1900, 1, 1);

    // Sortability is per column and was established by probe: the service silently ignores an order
    // on id, personId and deletedAt, which would show as a sort that does nothing. personName is not
    // a field of this service at all, so it can never be sorted on.
    public ResourceDescriptor Descriptor { get; } = new(
        ResourceKey,
        "Employees",
        "Numo.Employee.Api",
        [
            new ColumnDescriptor("personName", "Person", FieldKind.Text, IsSortable: false),
            new ColumnDescriptor(PersonIdFilterKey, "Person id", FieldKind.Guid, IsSortable: false),
            new ColumnDescriptor("code", "Code", FieldKind.Text, IsSortable: true),
            new ColumnDescriptor("email", "Work email", FieldKind.Text, IsSortable: true),
            new ColumnDescriptor("phone", "Work phone", FieldKind.Text, IsSortable: true),
        ],
        [
            new FilterDescriptor(PersonNameFilterKey, "Person name contains", FilterKind.Text, Options: null),
            new FilterDescriptor(PersonIdFilterKey, "Person id", FilterKind.Guid, Options: null),
            new FilterDescriptor(IncludeDeletedFilterKey, "Include deleted", FilterKind.Boolean, Options: null),
        ]);

    public async Task<ResourcePage> GetPageAsync(ResourceQuery query, CancellationToken cancellationToken)
    {
        // Resolved once rather than inside the fetch, which the page builder calls twice.
        var restriction = await ResolvePersonRestrictionAsync(query);

        return await ResourcePageBuilder.BuildAsync(
            query,
            (page, pageSize) => FetchPageAsync(query, restriction, page, pageSize),
            ToRowsAsync,
            restriction.Notice);
    }

    public async Task<ResourceRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var employee = await DownstreamCall.FindResultAsync(
            () => employeeClient.GetEmployee(id),
            id,
            $"{ResourceKey} record");

        if (employee is null)
        {
            return null;
        }

        var namesByPersonId = await personNameLookup.GetNamesByPersonIdAsync([employee.PersonId]);

        return ToRecord(employee, namesByPersonId.GetValueOrDefault(employee.PersonId));
    }

    /// <summary>
    /// Which people the page is restricted to, if any. The personName filter is a pre-search against
    /// the Person service because the Employee service cannot search a name; combined with an
    /// explicit personId it narrows to the intersection, so neither filter is quietly dropped.
    /// </summary>
    private async Task<PersonRestriction> ResolvePersonRestrictionAsync(ResourceQuery query)
    {
        var personId = GuidFilter(query, PersonIdFilterKey);
        var nameFragment = TextFilter(query, PersonNameFilterKey);

        if (nameFragment is null)
        {
            return personId is null
                ? PersonRestriction.None
                : new PersonRestriction([personId.Value], Notice: null);
        }

        var search = await personNameLookup.FindPersonIdsByNameAsync(nameFragment);

        var personIds = personId is null
            ? search.PersonIds
            : search.PersonIds.Where(id => id == personId.Value).ToList();

        return new PersonRestriction(personIds, search.IsTruncated ? TruncatedNotice() : null);
    }

    private static string TruncatedNotice()
        => $"More than {PersonNameLookup.MaxIdsPerCall} people match that name. "
            + $"Showing the employees of the first {PersonNameLookup.MaxIdsPerCall} only.";

    private Task<IEnumerable<EmployeeDto>> FetchPageAsync(
        ResourceQuery query,
        PersonRestriction restriction,
        int page,
        int pageSize)
    {
        if (restriction.MatchesNobody)
        {
            return Task.FromResult(Enumerable.Empty<EmployeeDto>());
        }

        // Page and PageSize are always set: an unset filter is a full-table read.
        var filter = new EmployeeFilter
        {
            Page = page,
            PageSize = pageSize,
            OrderBy = BuildOrderBy(query),
            PersonIds = restriction.PersonIds?.ToArray(),
            IncludeDeletedSince = BooleanFilter(query, IncludeDeletedFilterKey) is true
                ? IncludeDeletedSince
                : null,
        };

        return DownstreamCall.InvokeResultAsync(
            () => employeeClient.GetEmployees(filter),
            $"{ResourceKey} page {page}");
    }

    /// <summary>One name lookup for the whole page, which is the only reason the page-level
    /// projection exists.</summary>
    private async Task<IReadOnlyList<ResourceRow>> ToRowsAsync(IReadOnlyList<EmployeeDto> employees)
    {
        var namesByPersonId = await personNameLookup.GetNamesByPersonIdAsync(
            employees.Select(employee => employee.PersonId));

        return employees
            .Select(employee => ToRow(employee, namesByPersonId.GetValueOrDefault(employee.PersonId)))
            .ToList();
    }

    /// <summary>An empty list is serialised as no OrderBy parameter at all, which is required: a
    /// blank <c>OrderBy=</c> is not a syntax either service accepts.</summary>
    private static NumoOrderByList BuildOrderBy(ResourceQuery query)
    {
        var orderBy = new NumoOrderByList();

        if (!string.IsNullOrWhiteSpace(query.SortColumn))
        {
            orderBy.Add(
                query.SortColumn,
                query.IsSortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending);
        }

        return orderBy;
    }

    private static string? TextFilter(ResourceQuery query, string key)
        => query.Filters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static bool? BooleanFilter(ResourceQuery query, string key)
        => query.Filters.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : null;

    private static Guid? GuidFilter(ResourceQuery query, string key)
        => query.Filters.TryGetValue(key, out var value) && Guid.TryParse(value, out var parsed) ? parsed : null;

    // Cells are positional: this order is the Descriptor.Columns order. A person the lookup did not
    // return leaves the name cell empty rather than carrying invented text, and the person id cell
    // keeps its link so the row stays navigable either way.
    private static ResourceRow ToRow(EmployeeDto employee, string? personName)
        => new(
            employee.Id,
            employee.DeletedAt,
            [
                new Cell(personName, Link: null),
                new Cell(employee.PersonId.ToString(), PersonLink(employee)),
                new Cell(employee.Code, Link: null),
                new Cell(employee.WorkEmail, Link: null),
                new Cell(employee.WorkPhone, Link: null),
            ]);

    private static ResourceRecord ToRecord(EmployeeDto employee, string? personName)
        => new(
            employee.Id,
            Title(employee, personName),
            [
                new FieldValue("Id", employee.Id.ToString(), FieldKind.Guid, Link: null),
                new FieldValue("Person", personName, FieldKind.Text, Link: null),
                new FieldValue("Person id", employee.PersonId.ToString(), FieldKind.Guid, PersonLink(employee)),
                new FieldValue("Code", employee.Code, FieldKind.Text, Link: null),
                new FieldValue("Work email", employee.WorkEmail, FieldKind.Text, Link: null),
                new FieldValue("Work phone", employee.WorkPhone, FieldKind.Text, Link: null),
                new FieldValue("Deleted at", Format(employee.DeletedAt), FieldKind.DateTime, Link: null),
            ],
            [
                new RelationDescriptor("Positions", "positions", "employeeIds", employee.Id.ToString()),
                new RelationDescriptor("Absences", "absences", "employeeIds", employee.Id.ToString()),
                new RelationDescriptor("Department roles", "department-roles", "employeeId", employee.Id.ToString()),
            ]);

    private static RecordLink PersonLink(EmployeeDto employee)
        => new(PersonsResourceKey, employee.PersonId);

    private static string Title(EmployeeDto employee, string? personName)
        => personName ?? (string.IsNullOrEmpty(employee.Code) ? employee.Id.ToString() : employee.Code);

    private static string? Format(DateTimeOffset? value)
        => value?.ToString("O", CultureInfo.InvariantCulture);

    /// <param name="PersonIds">Null when the query restricts nothing. An empty list is not the same
    /// thing: an empty PersonIds array is ignored downstream, so it would read the whole table
    /// instead of nothing, which is what <see cref="MatchesNobody"/> exists to prevent.</param>
    private sealed record PersonRestriction(IReadOnlyList<Guid>? PersonIds, string? Notice)
    {
        public static readonly PersonRestriction None = new(PersonIds: null, Notice: null);

        public bool MatchesNobody => PersonIds is { Count: 0 };
    }
}
