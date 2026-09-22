using Numo.Employee.Lib.Clients.Department;
using Numo.Employee.Lib.Clients.Employee;
using Numo.Employee.Lib.Clients.JobTitle;
using Numo.Employee.Lib.Clients.Position;
using Numo.Employee.Lib.Models;

namespace Numo.DevServices.Api.Features.ServiceData.Resources;

/// <summary>
/// The Employee service's positions, and the resource the several-calls-per-method abstraction exists
/// for. The list reads <c>/api/positions/view</c>, whose rows embed the employee, department and job
/// title, so those cells show names for free; only the person name costs a call, batched once per page.
/// A single position has neither a view route nor an <c>Ids</c> filter, so the detail deliberately
/// fans out to the employee, department, job title and person instead.
///
/// Every call is a GET, and here that is stricter than elsewhere in the slice: the client library
/// switches to <c>POST {endpoint}/search</c> above 1000 characters of query string, and
/// <c>/api/positions/view/search</c> does not exist, so an overflow is a 404 rather than a silent
/// POST. <see cref="MaxPreSearchIds"/> is that budget.
/// </summary>
public sealed class PositionsResource(
    IPositionClient positionClient,
    IEmployeeClient employeeClient,
    IDepartmentClient departmentClient,
    IJobTitleClient jobTitleClient,
    PersonNameLookup personNameLookup)
    : IServiceDataResource
{
    private const string ResourceKey = "positions";
    private const string PersonsResourceKey = "persons";
    private const string EmployeesResourceKey = "employees";
    private const string DepartmentsResourceKey = "departments";
    private const string JobTitlesResourceKey = "job-titles";
    private const string AbsencesResourceKey = "absences";

    private const string PersonNameFilterKey = "personName";
    private const string EmployeeIdsFilterKey = "employeeIds";
    private const string DepartmentIdsFilterKey = "departmentIds";
    private const string PrimaryFilterKey = "primary";
    private const string FromFilterKey = "from";
    private const string TillFilterKey = "till";
    private const string IncludeDeletedFilterKey = "includeDeleted";
    private const string LegalRelationIdFilterKey = "legalRelationId";

    /// <summary>
    /// How many person ids the personName pre-search may return. This is not the employees
    /// resource's number: the same ids are spent in a filter carrying four more parameters, on the
    /// one route with no search fallback.
    ///
    /// The widest GET this descriptor can produce is 115 characters of scalars (paging at
    /// MaxPageSize, OrderBy=-activeFrom, primary, from, till, IncludeDeletedSince) plus 47 per
    /// person id and 100 per pair of employeeIds and departmentIds values, measured by calling
    /// PositionFilter's own ToQueryString. At <see cref="ResourceQueryFilters.MaxListValues"/> that
    /// is 115 + 400 + 47n, so eight ids cost 891 of the 1000 available and nine cost 938. Eight is
    /// the pair chosen, with 109 characters of headroom.
    /// </summary>
    private const int MaxPreSearchIds = 8;

    // Sortability is per column and was established by probe against /api/positions/view: the
    // service orders on code, activeFrom and activeTo and silently ignores every other column,
    // including the three embedded names, which are not fields of this route at all.
    public ResourceDescriptor Descriptor { get; } = new(
        ResourceKey,
        "Positions",
        "Numo.Employee.Api",
        ResourceSection.Personnel,
        [
            new ColumnDescriptor("personName", "Person", FieldKind.Text, IsSortable: false),
            new ColumnDescriptor("employeeCode", "Employee", FieldKind.Text, IsSortable: false),
            new ColumnDescriptor("departmentName", "Department", FieldKind.Text, IsSortable: false),
            new ColumnDescriptor("jobTitleName", "Job title", FieldKind.Text, IsSortable: false),
            new ColumnDescriptor("code", "Code", FieldKind.Text, IsSortable: true),
            new ColumnDescriptor("activeFrom", "Active from", FieldKind.Date, IsSortable: true),
            new ColumnDescriptor("activeTo", "Active to", FieldKind.Date, IsSortable: true),
            new ColumnDescriptor("workload", "Workload", FieldKind.Number, IsSortable: false),
            new ColumnDescriptor("primary", "Primary", FieldKind.Boolean, IsSortable: false),
            new ColumnDescriptor("schedule", "Schedule", FieldKind.Enum, IsSortable: false),
            new ColumnDescriptor("payType", "Pay type", FieldKind.Enum, IsSortable: false),
        ],
        [
            new FilterDescriptor(PersonNameFilterKey, "Person name contains", FilterKind.Text, Options: null),
            new FilterDescriptor(
                EmployeeIdsFilterKey,
                $"Employee ids (up to {ResourceQueryFilters.MaxListValues}, comma-separated)",
                FilterKind.GuidList,
                Options: null),
            new FilterDescriptor(
                DepartmentIdsFilterKey,
                $"Department ids (up to {ResourceQueryFilters.MaxListValues}, comma-separated)",
                FilterKind.GuidList,
                Options: null),
            new FilterDescriptor(PrimaryFilterKey, "Primary", FilterKind.Boolean, Options: null),
            new FilterDescriptor(FromFilterKey, "Active on or after", FilterKind.Date, Options: null),
            new FilterDescriptor(TillFilterKey, "Active on or before", FilterKind.Date, Options: null),
            new FilterDescriptor(IncludeDeletedFilterKey, "Include deleted", FilterKind.Boolean, Options: null),
            new FilterDescriptor(LegalRelationIdFilterKey, "Legal relation id", FilterKind.Guid, Options: null),
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

    /// <summary>
    /// The fan-out the absent by-id view route forces: the position, then its employee, department
    /// and job title, then the employee's person for a name. The department and job title do not
    /// depend on the employee, so the three run together and only the person waits.
    /// </summary>
    public async Task<ResourceRecord?> GetByIdAsync(
        Guid id,
        IReadOnlyDictionary<string, string> filters,
        CancellationToken cancellationToken)
    {
        var position = await DownstreamCall.FindResultAsync(
            () => positionClient.GetPosition(id),
            id,
            $"{ResourceKey} record");

        if (position is null)
        {
            return null;
        }

        var employeeTask = FindEmployeeWithNameAsync(position.EmployeeId);
        var departmentTask = FindDepartmentAsync(position.DepartmentId);
        var jobTitleTask = FindJobTitleAsync(position.JobTitleId);

        await Task.WhenAll(employeeTask, departmentTask, jobTitleTask);

        var (employee, personName) = await employeeTask;

        return ToRecord(
            position,
            new PositionRelations(employee, personName, await departmentTask, await jobTitleTask));
    }

    /// <summary>
    /// Which people the page is restricted to, if any. The personName filter is a pre-search against
    /// the Person service because the Employee service holds no name at all.
    /// </summary>
    private async Task<PersonRestriction> ResolvePersonRestrictionAsync(ResourceQuery query)
    {
        var nameFragment = ResourceQueryFilters.ReadText(query, PersonNameFilterKey);

        if (nameFragment is null)
        {
            return PersonRestriction.None;
        }

        var search = await personNameLookup.FindPersonIdsByNameAsync(
            nameFragment,
            narrowToPersonId: null,
            MaxPreSearchIds);

        return new PersonRestriction(search.PersonIds, search.IsTruncated ? TruncatedNotice() : null);
    }

    private static string TruncatedNotice()
        => $"More than {MaxPreSearchIds} people match that name. "
            + $"Showing the positions of the first {MaxPreSearchIds} only.";

    private Task<IEnumerable<PositionsView>> FetchPageAsync(
        ResourceQuery query,
        PersonRestriction restriction,
        int page,
        int pageSize)
    {
        if (restriction.MatchesNobody)
        {
            return Task.FromResult(Enumerable.Empty<PositionsView>());
        }

        // Page and PageSize are always set: an unset filter is a full-table read.
        var filter = new PositionFilter
        {
            Page = page,
            PageSize = pageSize,
            OrderBy = EmployeeOrderBy.From(query),
            PersonIds = restriction.PersonIds?.ToArray(),
            EmployeeIds = ResourceQueryFilters.ReadGuids(query, EmployeeIdsFilterKey)?.ToArray(),
            DepartmentIds = ResourceQueryFilters.ReadGuids(query, DepartmentIdsFilterKey)?.ToArray(),
            Primary = ResourceQueryFilters.ReadBoolean(query, PrimaryFilterKey),
            From = ResourceQueryFilters.ReadDate(query, FromFilterKey),
            Till = ResourceQueryFilters.ReadDate(query, TillFilterKey),
            IncludeDeletedSince = ResourceQueryFilters.ReadIncludeDeletedSince(query, IncludeDeletedFilterKey),
            LegalRelationId = ResourceQueryFilters.ReadGuid(query, LegalRelationIdFilterKey),
        };

        return DownstreamCall.InvokeResultAsync(
            () => positionClient.GetPositionsView(filter),
            $"{ResourceKey} page {page}");
    }

    /// <summary>One name lookup for the whole page. The embedded objects cost nothing, so this is the
    /// only call a row adds.</summary>
    private async Task<IReadOnlyList<ResourceRow>> ToRowsAsync(IReadOnlyList<PositionsView> positions)
    {
        var namesByPersonId = await personNameLookup.GetNamesByPersonIdAsync(positions.Select(PersonIdOf));

        return positions
            .Select(position => ToRow(
                position,
                new PositionRelations(
                    position.Employee,
                    namesByPersonId.GetValueOrDefault(PersonIdOf(position)),
                    position.Department,
                    position.JobTitle)))
            .ToList();
    }

    /// <summary>A position carries no person id of its own: it is reachable only through the
    /// employee, so an absent employee leaves the row nameless.</summary>
    private static Guid PersonIdOf(PositionsView position)
        => position.Employee?.PersonId ?? Guid.Empty;

    private async Task<(EmployeeDto? Employee, string? PersonName)> FindEmployeeWithNameAsync(Guid employeeId)
    {
        var employee = await DownstreamCall.FindResultAsync(
            () => employeeClient.GetEmployee(employeeId),
            employeeId,
            $"the employee of a {ResourceKey} record");

        if (employee is null)
        {
            return (null, null);
        }

        var namesByPersonId = await personNameLookup.GetNamesByPersonIdAsync([employee.PersonId]);

        return (employee, namesByPersonId.GetValueOrDefault(employee.PersonId));
    }

    private Task<DepartmentDto?> FindDepartmentAsync(Guid departmentId)
        => DownstreamCall.FindResultAsync(
            () => departmentClient.GetDepartment(departmentId),
            departmentId,
            $"the department of a {ResourceKey} record");

    private Task<JobTitleDto?> FindJobTitleAsync(Guid jobTitleId)
        => DownstreamCall.FindResultAsync(
            () => jobTitleClient.GetJobTitle(jobTitleId),
            jobTitleId,
            $"the job title of a {ResourceKey} record");

    // Cells are positional: this order is the Descriptor.Columns order.
    private static ResourceRow ToRow(PositionDto position, PositionRelations related)
        => new(
            position.Id,
            position.DeletedAt,
            [
                new Cell(related.PersonName, PersonLink(related)),
                new Cell(NameOrId(related.Employee?.Code, position.EmployeeId), EmployeeLink(position)),
                new Cell(NameOrId(related.Department?.Name, position.DepartmentId), DepartmentLink(position)),
                new Cell(NameOrId(related.JobTitle?.Name, position.JobTitleId), JobTitleLink(position)),
                new Cell(position.Code, Link: null),
                new Cell(FieldFormat.Format(position.ActiveFrom), Link: null),
                new Cell(FieldFormat.Format(position.ActiveTo), Link: null),
                new Cell(FieldFormat.FormatNumber(position.Workload), Link: null),
                new Cell(FieldFormat.Format(position.Primary), Link: null),
                new Cell(FieldFormat.Format(position.Schedule), Link: null),
                new Cell(FieldFormat.Format(position.PayType), Link: null),
            ]);

    // Field links cover the employee, department and job title - each a single record a position
    // has exactly one of. LegalRelationId is different: Numo.Employee.Lib has no LegalRelation
    // entity or client at all, only this bare id, shared with AbsenceDto. There is nothing to link
    // to, but positions and absences can both be filtered by it - so it becomes two relations
    // (a filtered grid of many) rather than a link (a single other record) or dead text.
    private static ResourceRecord ToRecord(PositionDto position, PositionRelations related)
        => new(
            position.Id,
            Title(position, related),
            [
                new FieldValue("Id", position.Id.ToString(), FieldKind.Guid, Link: null),
                new FieldValue("Person", related.PersonName, FieldKind.Text, PersonLink(related)),
                new FieldValue(
                    "Employee",
                    NameOrId(related.Employee?.Code, position.EmployeeId),
                    FieldKind.Text,
                    EmployeeLink(position)),
                new FieldValue(
                    "Department",
                    NameOrId(related.Department?.Name, position.DepartmentId),
                    FieldKind.Text,
                    DepartmentLink(position)),
                new FieldValue(
                    "Job title",
                    NameOrId(related.JobTitle?.Name, position.JobTitleId),
                    FieldKind.Text,
                    JobTitleLink(position)),
                new FieldValue("Code", position.Code, FieldKind.Text, Link: null),
                new FieldValue("Active from", FieldFormat.Format(position.ActiveFrom), FieldKind.Date, Link: null),
                new FieldValue("Active to", FieldFormat.Format(position.ActiveTo), FieldKind.Date, Link: null),
                new FieldValue("Workload", FieldFormat.FormatNumber(position.Workload), FieldKind.Number, Link: null),
                new FieldValue("Primary", FieldFormat.Format(position.Primary), FieldKind.Boolean, Link: null),
                new FieldValue("Use plan", FieldFormat.Format(position.UsePlan), FieldKind.Boolean, Link: null),
                new FieldValue("Schedule", FieldFormat.Format(position.Schedule), FieldKind.Enum, Link: null),
                new FieldValue("Pay type", FieldFormat.Format(position.PayType), FieldKind.Enum, Link: null),
                new FieldValue(
                    "Legal relation id",
                    position.LegalRelationId?.ToString(),
                    FieldKind.Guid,
                    Link: null),
                // Not another position's id, whatever the DTO calls it: probed,
                // /api/positions/{positionId} answers "not found" for every value seen in this
                // tenant, so the label says nothing it cannot back up and there is no link.
                new FieldValue(
                    "External position id",
                    position.PositionId?.ToString(),
                    FieldKind.Guid,
                    Link: null),
                new FieldValue("Deleted at", FieldFormat.Format(position.DeletedAt), FieldKind.DateTime, Link: null),
            ],
            LegalRelationRelations(position.LegalRelationId));

    private static IReadOnlyList<RelationDescriptor> LegalRelationRelations(Guid? legalRelationId)
        => legalRelationId is null
            ? []
            :
            [
                RelationDescriptor.To("Positions", ResourceKey, LegalRelationIdFilterKey, legalRelationId.Value.ToString()),
                RelationDescriptor.To("Absences", AbsencesResourceKey, LegalRelationIdFilterKey, legalRelationId.Value.ToString()),
            ];

    /// <summary>
    /// A related object the service did not return leaves the row showing the foreign key it has.
    /// That is not invented text - it is the position's own id column - and it keeps the cell's link
    /// clickable, which is the whole point of the column in a tool for looking things up.
    /// </summary>
    private static string NameOrId(string? name, Guid id)
        => string.IsNullOrWhiteSpace(name) ? id.ToString() : name;

    /// <summary>Null when the employee is absent, because the person id lives only there.</summary>
    private static RecordLink? PersonLink(PositionRelations related)
        => related.Employee is null ? null : new RecordLink(PersonsResourceKey, related.Employee.PersonId);

    private static RecordLink EmployeeLink(PositionDto position)
        => new(EmployeesResourceKey, position.EmployeeId);

    private static RecordLink DepartmentLink(PositionDto position)
        => new(DepartmentsResourceKey, position.DepartmentId);

    private static RecordLink JobTitleLink(PositionDto position)
        => new(JobTitlesResourceKey, position.JobTitleId);

    private static string Title(PositionDto position, PositionRelations related)
    {
        var jobTitle = related.JobTitle?.Name;
        var person = related.PersonName;

        return (person, jobTitle) switch
        {
            (not null, not null) => $"{person} - {jobTitle}",
            (not null, null) => person,
            (null, not null) => jobTitle,
            _ => string.IsNullOrEmpty(position.Code) ? position.Id.ToString() : position.Code,
        };
    }

    /// <param name="Employee">Null when the service did not return it, which a soft-deleted or
    /// removed related record makes possible. Every consumer below treats that as a missing label
    /// rather than a broken row.</param>
    private sealed record PositionRelations(
        EmployeeDto? Employee,
        string? PersonName,
        DepartmentDto? Department,
        JobTitleDto? JobTitle);
}
