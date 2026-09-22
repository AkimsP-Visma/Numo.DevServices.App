using Numo.Employee.Lib.Clients.Department;
using Numo.Employee.Lib.Models;

namespace Numo.DevServices.Api.Features.ServiceData.Resources;

/// <summary>
/// The Employee service's departments. The one resource whose children come from a call of their own:
/// <c>DepartmentFilter</c> has no ParentId, so a filtered grid of children cannot be expressed and
/// <c>GetDepartmentHierarchy</c> answers the question instead. Its result is a subtree, so the direct
/// children are the nodes whose ParentId is this department, and they leave as linked fields rather
/// than as a relation: a relation would have to pass their ids through the slice's list cap and would
/// quietly drop the rest.
/// </summary>
public sealed class DepartmentsResource(IDepartmentClient departmentClient) : IServiceDataResource
{
    private const string ResourceKey = "departments";
    private const string PositionsResourceKey = "positions";
    private const string AbsencesResourceKey = "absences";
    private const string DepartmentRolesResourceKey = "department-roles";

    private const string ActiveFromFilterKey = "activeFrom";
    private const string ActiveToFilterKey = "activeTo";

    // A column key is sent as OrderBy and a filter key as a filter parameter. They spell the
    // same thing here, but they are separate downstream contracts, so they are named apart.
    private const string ActiveFromColumnKey = "activeFrom";
    private const string ActiveToColumnKey = "activeTo";
    private const string IncludeDeletedFilterKey = "includeDeleted";

    /// <summary>How many children one record lists. A department at the top of a deep tree has as
    /// many as the tenant has departments, and a record is a page of fields, not a grid.</summary>
    private const int MaxChildrenListed = 20;

    /// <summary>
    /// The hierarchy route takes the permission its caller must hold. Null asks for no permission
    /// check, which is what a read-only browser wants and what it was probed with.
    /// </summary>
    private const string? NoRequiredPermission = null;

    // Only a column probed to actually sort is marked sortable: the service silently ignores an
    // order on a column it does not mark orderable, which would show as a sort that does nothing.
    // Name, activeFrom and activeTo sort; parentId was not offered and so was never probed.
    public ResourceDescriptor Descriptor { get; } = new(
        ResourceKey,
        "Departments",
        "Numo.Employee.Api",
        ResourceSection.Personnel,
        [
            new ColumnDescriptor("name", "Name", FieldKind.Text, IsSortable: true),
            new ColumnDescriptor("parentId", "Parent id", FieldKind.Guid, IsSortable: false),
            new ColumnDescriptor(ActiveFromColumnKey, "Active from", FieldKind.Date, IsSortable: true),
            new ColumnDescriptor(ActiveToColumnKey, "Active to", FieldKind.Date, IsSortable: true),
        ],
        [
            new FilterDescriptor(ActiveFromFilterKey, "Active on or after", FilterKind.Date, Options: null),
            new FilterDescriptor(ActiveToFilterKey, "Active on or before", FilterKind.Date, Options: null),
            new FilterDescriptor(IncludeDeletedFilterKey, "Include deleted", FilterKind.Boolean, Options: null),
        ]);

    public Task<ResourcePage> GetPageAsync(ResourceQuery query, CancellationToken cancellationToken)
        => ResourcePageBuilder.BuildAsync(
            query,
            (page, pageSize) => FetchPageAsync(query, page, pageSize),
            ToRow);

    public async Task<ResourceRecord?> GetByIdAsync(
        Guid id,
        IReadOnlyDictionary<string, string> filters,
        CancellationToken cancellationToken)
    {
        var department = await DownstreamCall.FindResultAsync(
            () => departmentClient.GetDepartment(id),
            id,
            $"{ResourceKey} record");

        if (department is null)
        {
            return null;
        }

        return ToRecord(department, await GetChildrenAsync(id));
    }

    private Task<IEnumerable<DepartmentDto>> FetchPageAsync(ResourceQuery query, int page, int pageSize)
    {
        // Page and PageSize are always set: an unset filter is a full-table read.
        var filter = new DepartmentFilter
        {
            Page = page,
            PageSize = pageSize,
            OrderBy = EmployeeOrderBy.From(query),
            ActiveFrom = ResourceQueryFilters.ReadDate(query, ActiveFromFilterKey),
            ActiveTo = ResourceQueryFilters.ReadDate(query, ActiveToFilterKey),
            IncludeDeletedSince = ResourceQueryFilters.ReadIncludeDeletedSince(query, IncludeDeletedFilterKey),
        };

        return DownstreamCall.InvokeResultAsync(
            () => departmentClient.GetDepartments(filter),
            $"{ResourceKey} page {page}");
    }

    /// <summary>The direct children only: the call answers with the whole subtree, and a grandchild
    /// belongs on the record of its own parent.</summary>
    private async Task<IReadOnlyList<DepartmentHierarchyDto>> GetChildrenAsync(Guid id)
    {
        var subtree = await DownstreamCall.InvokeResultAsync(
            () => departmentClient.GetDepartmentHierarchy(id, NoRequiredPermission),
            $"the children of a {ResourceKey} record");

        return subtree
            .Where(node => node.ParentId == id)
            .OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // Cells are positional: this order is the Descriptor.Columns order.
    private static ResourceRow ToRow(DepartmentDto department)
        => new(
            department.Id,
            department.DeletedAt,
            [
                new Cell(department.Name, Link: null),
                new Cell(department.ParentId?.ToString(), ParentLink(department)),
                new Cell(FieldFormat.Format(department.ActiveFrom), Link: null),
                new Cell(FieldFormat.Format(department.ActiveTo), Link: null),
            ]);

    private static ResourceRecord ToRecord(
        DepartmentDto department,
        IReadOnlyList<DepartmentHierarchyDto> children)
        => new(
            department.Id,
            string.IsNullOrWhiteSpace(department.Name) ? department.Id.ToString() : department.Name,
            [
                new FieldValue("Id", department.Id.ToString(), FieldKind.Guid, Link: null),
                new FieldValue("Name", department.Name, FieldKind.Text, Link: null),
                new FieldValue(
                    "Parent id",
                    department.ParentId?.ToString(),
                    FieldKind.Guid,
                    ParentLink(department)),
                new FieldValue("Active from", FieldFormat.Format(department.ActiveFrom), FieldKind.Date, Link: null),
                new FieldValue("Active to", FieldFormat.Format(department.ActiveTo), FieldKind.Date, Link: null),
                new FieldValue("Deleted at", FieldFormat.Format(department.DeletedAt), FieldKind.DateTime, Link: null),
                .. ChildFields(children),
            ],
            [
                RelationDescriptor.To("Positions", PositionsResourceKey, "departmentIds", department.Id.ToString()),
                RelationDescriptor.To("Absences", AbsencesResourceKey, "departmentIds", department.Id.ToString()),
                RelationDescriptor.To(
                    "Department roles",
                    DepartmentRolesResourceKey,
                    "departmentId",
                    department.Id.ToString()),
            ]);

    /// <summary>One linked field per direct child, and a stated count when there are more than a
    /// record can carry, so a truncated list never reads as a complete one.</summary>
    private static IEnumerable<FieldValue> ChildFields(IReadOnlyList<DepartmentHierarchyDto> children)
    {
        foreach (var child in children.Take(MaxChildrenListed))
        {
            yield return new FieldValue(
                "Child",
                string.IsNullOrWhiteSpace(child.Name) ? child.Id.ToString() : child.Name,
                FieldKind.Text,
                new RecordLink(ResourceKey, child.Id));
        }

        if (children.Count > MaxChildrenListed)
        {
            yield return new FieldValue(
                "Children not listed",
                (children.Count - MaxChildrenListed).ToString(),
                FieldKind.Number,
                Link: null);
        }
    }

    private static RecordLink? ParentLink(DepartmentDto department)
        => department.ParentId is null ? null : new RecordLink(ResourceKey, department.ParentId.Value);
}
