using Numo.Employee.Common.Enums;
using Numo.Employee.Lib.Clients.Absence;
using Numo.Employee.Lib.Models;

namespace Numo.DevServices.Api.Features.ServiceData.Resources;

/// <summary>
/// The Employee service's absences. The resource where the enum-as-name rendering earns the client
/// libraries: type and status cross the wire as integers and arrive here typed, so
/// <see cref="FieldFormat"/> renders their names without a table of our own, and the status filter
/// publishes the same names as its closed list.
///
/// A record links to its employee and its department rather than declaring relations to them: an
/// absence has exactly one of each, and a relation is a filtered grid of many.
/// </summary>
public sealed class AbsencesResource(IAbsenceClient absenceClient) : IServiceDataResource
{
    private const string ResourceKey = "absences";
    private const string EmployeesResourceKey = "employees";
    private const string DepartmentsResourceKey = "departments";

    private const string EmployeeIdsFilterKey = "employeeIds";
    private const string DepartmentIdsFilterKey = "departmentIds";
    private const string FromFilterKey = "from";
    private const string TillFilterKey = "till";
    private const string StatusesFilterKey = "statuses";
    private const string IncludeDeletedFilterKey = "includeDeleted";

    // Only a column probed to actually sort is marked sortable: the service silently ignores an
    // order on a column it does not mark orderable, which would show as a sort that does nothing.
    // From and till sort; the rest were not offered and so were never probed.
    public ResourceDescriptor Descriptor { get; } = new(
        ResourceKey,
        "Absences",
        "Numo.Employee.Api",
        [
            new ColumnDescriptor("employeeId", "Employee id", FieldKind.Guid, IsSortable: false),
            new ColumnDescriptor("departmentId", "Department id", FieldKind.Guid, IsSortable: false),
            new ColumnDescriptor(FromFilterKey, "From", FieldKind.Date, IsSortable: true),
            new ColumnDescriptor(TillFilterKey, "Till", FieldKind.Date, IsSortable: true),
            new ColumnDescriptor("type", "Type", FieldKind.Enum, IsSortable: false),
            new ColumnDescriptor("status", "Status", FieldKind.Enum, IsSortable: false),
            new ColumnDescriptor("payTypeCode", "Pay type code", FieldKind.Text, IsSortable: false),
        ],
        [
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
            new FilterDescriptor(FromFilterKey, "Absent on or after", FilterKind.Date, Options: null),
            new FilterDescriptor(TillFilterKey, "Absent on or before", FilterKind.Date, Options: null),
            // Named for the downstream array it fills, which takes several statuses; the wire
            // contract has no list-valued enum filter, so one status reaches it at a time.
            new FilterDescriptor(
                StatusesFilterKey,
                "Status",
                FilterKind.Enum,
                FieldFormat.Options<AbsenceStatus>()),
            new FilterDescriptor(IncludeDeletedFilterKey, "Include deleted", FilterKind.Boolean, Options: null),
        ]);

    public Task<ResourcePage> GetPageAsync(ResourceQuery query, CancellationToken cancellationToken)
        => ResourcePageBuilder.BuildAsync(
            query,
            (page, pageSize) => FetchPageAsync(query, page, pageSize),
            ToRow);

    public async Task<ResourceRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var absence = await DownstreamCall.FindResultAsync(
            () => absenceClient.GetAbsence(id),
            id,
            $"{ResourceKey} record");

        return absence is null ? null : ToRecord(absence);
    }

    private Task<IEnumerable<AbsenceDto>> FetchPageAsync(ResourceQuery query, int page, int pageSize)
    {
        var status = ResourceQueryFilters.ReadEnum<AbsenceStatus>(query, StatusesFilterKey);

        // Page and PageSize are always set: an unset filter is a full-table read.
        var filter = new AbsenceFilter
        {
            Page = page,
            PageSize = pageSize,
            OrderBy = EmployeeOrderBy.From(query),
            EmployeeIds = ResourceQueryFilters.ReadGuids(query, EmployeeIdsFilterKey)?.ToArray(),
            DepartmentIds = ResourceQueryFilters.ReadGuids(query, DepartmentIdsFilterKey)?.ToArray(),
            From = ResourceQueryFilters.ReadDate(query, FromFilterKey),
            Till = ResourceQueryFilters.ReadDate(query, TillFilterKey),
            // Null rather than an empty array: the service ignores an empty one and would answer
            // with the whole table instead of nothing.
            Statuses = status is null ? null : new[] { status.Value },
            IncludeDeletedSince = ResourceQueryFilters.ReadIncludeDeletedSince(query, IncludeDeletedFilterKey),
        };

        return DownstreamCall.InvokeResultAsync(
            () => absenceClient.GetAbsences(filter),
            $"{ResourceKey} page {page}");
    }

    // Cells are positional: this order is the Descriptor.Columns order. The two ids carry links
    // rather than resolved names: neither is a field of this route, so a name would cost a call per
    // page for a column the link already reaches.
    private static ResourceRow ToRow(AbsenceDto absence)
        => new(
            absence.Id,
            absence.DeletedAt,
            [
                new Cell(absence.EmployeeId.ToString(), EmployeeLink(absence)),
                new Cell(absence.DepartmentId.ToString(), DepartmentLink(absence)),
                new Cell(FieldFormat.Format(absence.From), Link: null),
                new Cell(FieldFormat.Format(absence.Till), Link: null),
                new Cell(FieldFormat.Format(absence.Type), Link: null),
                new Cell(FieldFormat.Format(absence.Status), Link: null),
                new Cell(absence.PayTypeCode, Link: null),
            ]);

    private static ResourceRecord ToRecord(AbsenceDto absence)
        => new(
            absence.Id,
            Title(absence),
            [
                new FieldValue("Id", absence.Id.ToString(), FieldKind.Guid, Link: null),
                new FieldValue(
                    "Employee id",
                    absence.EmployeeId.ToString(),
                    FieldKind.Guid,
                    EmployeeLink(absence)),
                new FieldValue(
                    "Department id",
                    absence.DepartmentId.ToString(),
                    FieldKind.Guid,
                    DepartmentLink(absence)),
                new FieldValue("From", FieldFormat.Format(absence.From), FieldKind.Date, Link: null),
                new FieldValue("Till", FieldFormat.Format(absence.Till), FieldKind.Date, Link: null),
                new FieldValue("Type", FieldFormat.Format(absence.Type), FieldKind.Enum, Link: null),
                new FieldValue("Status", FieldFormat.Format(absence.Status), FieldKind.Enum, Link: null),
                new FieldValue("Affects norm", FieldFormat.Format(absence.AffectsNorm), FieldKind.Boolean, Link: null),
                new FieldValue("Pay type code", absence.PayTypeCode, FieldKind.Text, Link: null),
                new FieldValue(
                    "Pay type calendar",
                    FieldFormat.Format(absence.PayTypeCalendar),
                    FieldKind.Enum,
                    Link: null),
                new FieldValue(
                    "Legal relation id",
                    absence.LegalRelationId?.ToString(),
                    FieldKind.Guid,
                    Link: null),
                new FieldValue("Deleted at", FieldFormat.Format(absence.DeletedAt), FieldKind.DateTime, Link: null),
            ],
            []);

    private static RecordLink EmployeeLink(AbsenceDto absence)
        => new(EmployeesResourceKey, absence.EmployeeId);

    private static RecordLink DepartmentLink(AbsenceDto absence)
        => new(DepartmentsResourceKey, absence.DepartmentId);

    /// <summary>No name is reachable without another call, so the title says what the absence is and
    /// when, which is what distinguishes one row of a filtered grid from the next.</summary>
    private static string Title(AbsenceDto absence)
        => $"{FieldFormat.Format(absence.Type)} from {FieldFormat.Format(absence.From)}";
}
