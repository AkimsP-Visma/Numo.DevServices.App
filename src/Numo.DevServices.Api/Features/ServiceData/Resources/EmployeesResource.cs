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

    // A column key is sent as OrderBy and a filter key as a filter parameter. They spell the
    // same thing here, but they are separate downstream contracts, so they are named apart.
    private const string PersonIdColumnKey = "personId";

    /// <summary>
    /// How many person ids the personName pre-search may return. Budget: an employees GET carrying
    /// these ids plus paging, OrderBy and IncludeDeletedSince, measured at 911 characters for 18 ids
    /// against the 1000 at which the client would fall back to a POST search. It is this resource's
    /// number and not the lookup's, because the other parameters sharing the query string are this
    /// resource's own.
    /// </summary>
    private const int MaxPreSearchIds = 18;

    // Sortability is per column and was established by probe: the service silently ignores an order
    // on id, personId and deletedAt, which would show as a sort that does nothing. personName is not
    // a field of this service at all, so it can never be sorted on.
    public ResourceDescriptor Descriptor { get; } = new(
        ResourceKey,
        "Employees",
        "Numo.Employee.Api",
        [
            new ColumnDescriptor("personName", "Person", FieldKind.Text, IsSortable: false),
            new ColumnDescriptor(PersonIdColumnKey, "Person id", FieldKind.Guid, IsSortable: false),
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
        var personId = ResourceQueryFilters.ReadGuid(query, PersonIdFilterKey);
        var nameFragment = ResourceQueryFilters.ReadText(query, PersonNameFilterKey);

        if (nameFragment is null)
        {
            return personId is null
                ? PersonRestriction.None
                : new PersonRestriction([personId.Value], Notice: null);
        }

        // Both filters go into the one pre-search rather than being intersected here: intersecting
        // locally against a capped list would report nobody whenever the named person fell outside
        // the first page of matches.
        var search = await personNameLookup.FindPersonIdsByNameAsync(nameFragment, personId, MaxPreSearchIds);

        return new PersonRestriction(search.PersonIds, search.IsTruncated ? TruncatedNotice() : null);
    }

    private static string TruncatedNotice()
        => $"More than {MaxPreSearchIds} people match that name. "
            + $"Showing the employees of the first {MaxPreSearchIds} only.";

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
            OrderBy = EmployeeOrderBy.From(query),
            PersonIds = restriction.PersonIds?.ToArray(),
            IncludeDeletedSince = ResourceQueryFilters.ReadIncludeDeletedSince(query, IncludeDeletedFilterKey),
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
                new FieldValue("Deleted at", FieldFormat.Format(employee.DeletedAt), FieldKind.DateTime, Link: null),
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
}
