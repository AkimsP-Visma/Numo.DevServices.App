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
    IReadOnlyList<ColumnDescriptor> Columns,
    IReadOnlyList<FilterDescriptor> Filters);

/// <summary><paramref name="IsSortable"/> is a per-resource fact established by probe: the services
/// silently ignore an order on a column they do not mark orderable.</summary>
public sealed record ColumnDescriptor(string Key, string Label, FieldKind Kind, bool IsSortable);

public sealed record FilterDescriptor(
    string Key,
    string Label,
    FilterKind Kind,
    IReadOnlyList<string>? Options);

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

public sealed record RelationDescriptor(
    string Label,
    string TargetResource,
    string FilterKey,
    string FilterValue);

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
}
