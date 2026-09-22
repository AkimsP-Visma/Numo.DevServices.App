namespace Numo.DevServices.Api.Features.ServiceData;

/// <summary>
/// The wire contract of the slice. The frontend renders nothing that is not described here, so a
/// resource that needs a shape this file does not offer is a deliberate edit to both ends.
/// Public because a public handler cannot take an internal parameter type (CS0051).
/// </summary>
public sealed record ResourceDescriptor(
    string Key,
    string Label,
    string ServiceName,
    ResourceSection Section,
    IReadOnlyList<ColumnDescriptor> Columns,
    IReadOnlyList<FilterDescriptor> Filters,
    bool RequiresTenant = true,
    bool IsReachableOnlyByRelation = false);

/// <summary>
/// Which nav entry's picker offers a resource. Declared per resource rather than inferred from
/// <see cref="ResourceDescriptor.ServiceName"/>: that happens to sort every current resource
/// correctly but says nothing about where a future service belongs.
/// </summary>
public enum ResourceSection
{
    Personnel,
    DataIntegration,
}

/// <summary><paramref name="IsSortable"/> is a per-resource fact established by probe: the services
/// silently ignore an order on a column they do not mark orderable.</summary>
public sealed record ColumnDescriptor(string Key, string Label, FieldKind Kind, bool IsSortable);

/// <param name="IsRequired">A nested resource's parent-id filter: the page validator rejects a
/// request missing it, so a resource is never asked to browse without the id its route needs.</param>
public sealed record FilterDescriptor(
    string Key,
    string Label,
    FilterKind Kind,
    IReadOnlyList<string>? Options,
    bool IsRequired = false);

/// <summary>
/// One page of rows. No total count exists anywhere in either service, so <paramref name="HasMore"/>
/// is all the paging state there can be. <paramref name="Notice"/> is how a resource says it
/// truncated something.
/// </summary>
public sealed record ResourcePage(
    IReadOnlyList<ResourceRow> Rows,
    int Page,
    int PageSize,
    bool HasMore,
    string? Notice);

public sealed record ResourceRow(Guid Id, DateTimeOffset? DeletedAt, IReadOnlyList<Cell> Cells);

/// <summary>A cell carries an optional link because grid-to-grid navigation cannot be expressed by a
/// plain string.</summary>
public sealed record Cell(string? Value, RecordLink? Link);

public sealed record ResourceRecord(
    Guid Id,
    string Title,
    IReadOnlyList<FieldValue> Fields,
    IReadOnlyList<RelationDescriptor> Relations);

public sealed record FieldValue(string Label, string? Value, FieldKind Kind, RecordLink? Link);

public sealed record RecordLink(string TargetResource, Guid Id);

/// <summary>
/// A button on a record that navigates to another resource's list, pre-filtered.
/// <paramref name="Filters"/> is the same shape as <see cref="ResourceQuery.Filters"/>: one entry
/// for nearly every relation, two for di-execution-step-dataset (executionId and stepId, the one
/// target reachable only by two parent ids at once). <see cref="To"/> is the convenience
/// constructor for the common one-filter case - a construction-time shorthand, not a second field
/// or a second way to represent a relation on the wire.
/// </summary>
public sealed record RelationDescriptor(
    string Label,
    string TargetResource,
    IReadOnlyDictionary<string, string> Filters)
{
    public static RelationDescriptor To(string label, string targetResource, string filterKey, string filterValue)
        => new(label, targetResource, new Dictionary<string, string> { [filterKey] = filterValue });
}

/// <summary><paramref name="Filters"/> is keyed by <see cref="FilterDescriptor.Key"/>, so a resource
/// reads only the filters it declared and an unknown key never reaches a service.</summary>
public sealed record ResourceQuery(
    int Page,
    int PageSize,
    string? SortColumn,
    bool IsSortDescending,
    IReadOnlyDictionary<string, string> Filters);

public enum FieldKind
{
    Text,
    Guid,
    Date,
    DateTime,
    Number,
    Boolean,
    Enum,
}

public enum FilterKind
{
    Text,
    Guid,
    Date,
    Boolean,
    Enum,

    /// <summary>Several ids in one value, delimited by
    /// <see cref="ResourceQueryFilters.ListDelimiter"/>. Needed because the Employee service's id
    /// filters are arrays, and a relation onto one of them cannot be expressed by a single id.
    /// </summary>
    GuidList,
}
