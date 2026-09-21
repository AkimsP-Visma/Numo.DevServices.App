using System.ComponentModel;
using System.Globalization;
using Numo.Person.Common.QuerySupport.Ordering;
using Numo.Person.Lib;
using Numo.Person.Lib.Exceptions;
using Numo.Person.Lib.Models;

namespace Numo.DevServices.Api.Features.ServiceData.Resources;

/// <summary>
/// The Person service's people. The model the other resources copy: the descriptor names every
/// column and filter the frontend may use, <see cref="IPersonClient"/> failures arrive as exceptions
/// and leave as <see cref="DownstreamCallException"/>, and paging is left to
/// <see cref="ResourcePageBuilder"/> so that only the filter and the row projection are written here.
/// </summary>
public sealed class PersonsResource(IPersonClient personClient) : IServiceDataResource
{
    private const string ResourceKey = "persons";
    private const string FullNamePartFilterKey = "fullNamePart";
    private const string EmailFilterKey = "email";
    private const string IsActiveFilterKey = "isActive";
    private const string IncludeDeletedFilterKey = "includeDeleted";

    /// <summary>
    /// The Person service excludes soft-deleted rows unless told how far back to include them, so
    /// including them means a date old enough to cover every row rather than a flag.
    /// </summary>
    private static readonly DateOnly IncludeDeletedSince = new(1900, 1, 1);

    // Sortability is per column and was established by probe: the service silently ignores an order
    // on any other column, which would show as a sort that does nothing.
    public ResourceDescriptor Descriptor { get; } = new(
        ResourceKey,
        "Persons",
        "Numo.Person.Api",
        [
            new ColumnDescriptor("firstName", "First name", FieldKind.Text, IsSortable: true),
            new ColumnDescriptor("lastName", "Last name", FieldKind.Text, IsSortable: true),
            new ColumnDescriptor("email", "Email", FieldKind.Text, IsSortable: true),
            new ColumnDescriptor("personCode", "Person code", FieldKind.Text, IsSortable: false),
            new ColumnDescriptor("phone", "Phone", FieldKind.Text, IsSortable: false),
            new ColumnDescriptor("isActive", "Active", FieldKind.Boolean, IsSortable: false),
            new ColumnDescriptor("statusChangedAt", "Status changed at", FieldKind.DateTime, IsSortable: false),
        ],
        [
            new FilterDescriptor(FullNamePartFilterKey, "Name contains", FilterKind.Text, Options: null),
            new FilterDescriptor(EmailFilterKey, "Email", FilterKind.Text, Options: null),
            new FilterDescriptor(IsActiveFilterKey, "Active", FilterKind.Boolean, Options: null),
            new FilterDescriptor(IncludeDeletedFilterKey, "Include deleted", FilterKind.Boolean, Options: null),
        ]);

    public Task<ResourcePage> GetPageAsync(ResourceQuery query, CancellationToken cancellationToken)
        => ResourcePageBuilder.BuildAsync(
            query,
            (page, pageSize) => FetchPageAsync(query, page, pageSize),
            ToRow);

    public async Task<ResourceRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var person = await DownstreamCall.FindAsync<PersonDto, PersonNotFoundException>(
            () => personClient.GetPerson(id),
            $"{ResourceKey} record");

        return person is null ? null : ToRecord(person);
    }

    private Task<IEnumerable<PersonDto>> FetchPageAsync(ResourceQuery query, int page, int pageSize)
    {
        // Page and PageSize are always set: an unset filter is a full-table read against a live service.
        var filter = new PersonFilter
        {
            Page = page,
            PageSize = pageSize,
            OrderBy = BuildOrderBy(query),
            FullNamePart = TextFilter(query, FullNamePartFilterKey),
            Email = TextFilter(query, EmailFilterKey),
            IsActive = BooleanFilter(query, IsActiveFilterKey),
            IncludeDeletedSince = BooleanFilter(query, IncludeDeletedFilterKey) is true
                ? IncludeDeletedSince
                : null,
        };

        return DownstreamCall.InvokeAsync(
            () => personClient.GetPersons(filter),
            $"{ResourceKey} page {page}");
    }

    /// <summary>An empty list is serialised as no OrderBy parameter at all, which is required: the
    /// service answers a blank <c>OrderBy=</c> with a failure.</summary>
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

    // Cells are positional: this order is the Descriptor.Columns order.
    private static ResourceRow ToRow(PersonDto person)
        => new(
            person.Id,
            person.DeletedAt,
            [
                new Cell(person.FirstName, Link: null),
                new Cell(person.LastName, Link: null),
                new Cell(person.Email, Link: null),
                new Cell(person.PersonCode, Link: null),
                new Cell(person.Phone, Link: null),
                new Cell(Format(person.IsActive), Link: null),
                new Cell(Format(person.StatusChangedAt), Link: null),
            ]);

    // A relation is declared once the resource on the other end exists, so that a link never points
    // at a resource key the catalogue does not hold.
    private static ResourceRecord ToRecord(PersonDto person)
        => new(
            person.Id,
            Title(person),
            [
                new FieldValue("Id", person.Id.ToString(), FieldKind.Guid, Link: null),
                new FieldValue("First name", person.FirstName, FieldKind.Text, Link: null),
                new FieldValue("Last name", person.LastName, FieldKind.Text, Link: null),
                new FieldValue("Email", person.Email, FieldKind.Text, Link: null),
                new FieldValue("Person code", person.PersonCode, FieldKind.Text, Link: null),
                new FieldValue("Phone", person.Phone, FieldKind.Text, Link: null),
                new FieldValue("Active", Format(person.IsActive), FieldKind.Boolean, Link: null),
                new FieldValue("Status changed at", Format(person.StatusChangedAt), FieldKind.DateTime, Link: null),
                new FieldValue("Deleted at", Format(person.DeletedAt), FieldKind.DateTime, Link: null),
            ],
            [
                new RelationDescriptor("Employees", "employees", "personId", person.Id.ToString()),
            ]);

    private static string Title(PersonDto person)
    {
        var name = $"{person.FirstName} {person.LastName}".Trim();
        return string.IsNullOrEmpty(name) ? person.Id.ToString() : name;
    }

    private static string Format(bool value)
        => value ? "true" : "false";

    private static string? Format(DateTimeOffset? value)
        => value?.ToString("O", CultureInfo.InvariantCulture);
}
