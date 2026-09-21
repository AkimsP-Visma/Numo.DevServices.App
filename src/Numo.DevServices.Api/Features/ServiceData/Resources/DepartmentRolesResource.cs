using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Numo.Common.Microservice.Lib.CurrentTenant;
using Numo.Employee.Common.Enums;

namespace Numo.DevServices.Api.Features.ServiceData.Resources;

// The one resource that does not go through a client library, and not a pattern to copy.
// Numo.Employee.Lib keeps DepartmentRoleDto internal and IDepartmentClient offers no method for the
// unfiltered roles route, so this resource owns its HTTP client, its own record, its own response
// envelope and its own Numo-Tenant-Id header - the header the library's handler pipeline would
// otherwise have added. Every other resource in this folder reaches its service through the library.

/// <summary>
/// The Employee service's department roles, read over a named <see cref="HttpClient"/> whose base
/// address comes from service discovery. Parsing the envelope here buys one thing the libraries
/// cannot: an absent record is recognised from the error's metadata rather than from the wording of
/// its message.
///
/// A record links to its department and its employee rather than declaring relations to them: a role
/// has exactly one of each, and a relation is a filtered grid of many.
/// </summary>
public sealed class DepartmentRolesResource(
    IHttpClientFactory httpClientFactory,
    INumoCurrentTenantService currentTenant) : IServiceDataResource
{
    private const string ResourceKey = "department-roles";
    private const string DepartmentsResourceKey = "departments";
    private const string EmployeesResourceKey = "employees";

    /// <summary>The service as the descriptor labels it for the UI, which is the same spelling
    /// <see cref="ServiceDataRegistration"/> looks up in service discovery.</summary>
    private const string ServiceName = "Numo.Employee.Api";

    // Singular, and not list-valued: the departments and employees resources each emit a relation
    // into these exact keys, and a rename or a plural leaves those buttons pointing at nothing.
    private const string DepartmentIdFilterKey = "departmentId";
    private const string EmployeeIdFilterKey = "employeeId";
    private const string ActiveFilterKey = "active";
    private const string RoleTypeFilterKey = "roleType";
    private const string IncludeDeletedFilterKey = "includeDeleted";

    // A column key is sent as OrderBy and a filter key as a filter parameter. They are separate
    // downstream contracts even where they spell the same thing, so they are named apart.
    private const string ActiveFromColumnKey = "activeFrom";
    private const string ActiveToColumnKey = "activeTo";

    /// <summary>The metadata field an absent record is recognised by, as the service spells it.</summary>
    private const string MissingIdParamName = "Id";

    /// <summary>Web defaults: the envelope and the payload are camel-cased.</summary>
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web);

    // Only a column probed to actually sort is marked sortable: the service silently ignores an order
    // on a column it does not mark orderable, which would show as a sort that does nothing. activeFrom
    // and activeTo sort; departmentId, employeeId and role were probed and are ignored.
    public ResourceDescriptor Descriptor { get; } = new(
        ResourceKey,
        "Department roles",
        ServiceName,
        [
            new ColumnDescriptor("departmentId", "Department id", FieldKind.Guid, IsSortable: false),
            new ColumnDescriptor("employeeId", "Employee id", FieldKind.Guid, IsSortable: false),
            new ColumnDescriptor("role", "Role", FieldKind.Enum, IsSortable: false),
            new ColumnDescriptor(ActiveFromColumnKey, "Active from", FieldKind.Date, IsSortable: true),
            new ColumnDescriptor(ActiveToColumnKey, "Active to", FieldKind.Date, IsSortable: true),
        ],
        [
            new FilterDescriptor(DepartmentIdFilterKey, "Department id", FilterKind.Guid, Options: null),
            new FilterDescriptor(EmployeeIdFilterKey, "Employee id", FilterKind.Guid, Options: null),
            new FilterDescriptor(ActiveFilterKey, "Active", FilterKind.Boolean, Options: null),
            new FilterDescriptor(
                RoleTypeFilterKey,
                "Role",
                FilterKind.Enum,
                FieldFormat.Options<DepartmentRoleType>()),
            new FilterDescriptor(IncludeDeletedFilterKey, "Include deleted", FilterKind.Boolean, Options: null),
        ]);

    public Task<ResourcePage> GetPageAsync(ResourceQuery query, CancellationToken cancellationToken)
        => ResourcePageBuilder.BuildAsync(
            query,
            (page, pageSize) => FetchPageAsync(query, page, pageSize, cancellationToken),
            ToRow);

    public async Task<ResourceRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var callDescription = $"{ResourceKey} record";

        var envelope = await GetEnvelopeAsync<DepartmentRole>(
            $"{ServiceDataRegistration.DepartmentRolesPath}/{id}",
            callDescription,
            cancellationToken);

        if (envelope.IsSuccessful && envelope.Value is not null)
        {
            return ToRecord(envelope.Value);
        }

        // The structured metadata the client libraries discard: an absence is the id that was asked
        // for coming back as the missing parameter, not a phrase matched inside a message.
        return IsRecordAbsent(envelope, id) ? null : throw Failed(envelope, callDescription);
    }

    private async Task<IEnumerable<DepartmentRole>> FetchPageAsync(
        ResourceQuery query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var callDescription = $"{ResourceKey} page {page}";

        var envelope = await GetEnvelopeAsync<List<DepartmentRole>>(
            BuildPageUrl(query, page, pageSize),
            callDescription,
            cancellationToken);

        if (!envelope.IsSuccessful || envelope.Value is null)
        {
            throw Failed(envelope, callDescription);
        }

        return envelope.Value;
    }

    /// <summary>
    /// Page and PageSize are always set: an unset page filter is a full-table read. OrderBy is left
    /// off the request entirely when no sort was asked for, because a blank one fails the request.
    /// </summary>
    private static string BuildPageUrl(ResourceQuery query, int page, int pageSize)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("Page", page.ToString()),
            new("PageSize", pageSize.ToString()),
        };

        AddParameter(parameters, "OrderBy", OrderBy(query));
        AddParameter(
            parameters,
            "DepartmentId",
            ResourceQueryFilters.ReadGuid(query, DepartmentIdFilterKey)?.ToString());
        AddParameter(
            parameters,
            "EmployeeId",
            ResourceQueryFilters.ReadGuid(query, EmployeeIdFilterKey)?.ToString());
        AddParameter(
            parameters,
            "Active",
            FieldFormat.Format(ResourceQueryFilters.ReadBoolean(query, ActiveFilterKey)));
        AddParameter(
            parameters,
            "RoleType",
            FieldFormat.Format(ResourceQueryFilters.ReadEnum<DepartmentRoleType>(query, RoleTypeFilterKey)));
        AddParameter(
            parameters,
            "IncludeDeletedSince",
            FieldFormat.Format(ResourceQueryFilters.ReadIncludeDeletedSince(query, IncludeDeletedFilterKey)));

        var queryString = string.Join(
            '&',
            parameters.Select(parameter => $"{parameter.Key}={Uri.EscapeDataString(parameter.Value)}"));

        return $"{ServiceDataRegistration.DepartmentRolesPath}?{queryString}";
    }

    private static void AddParameter(List<KeyValuePair<string, string>> parameters, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            parameters.Add(new KeyValuePair<string, string>(key, value));
        }
    }

    /// <summary>The service's own order syntax, probed: <c>field</c> ascending, <c>-field</c>
    /// descending, and nothing else understood. Null when no sort was asked for, which is what keeps
    /// the parameter off the request.</summary>
    private static string? OrderBy(ResourceQuery query)
        => string.IsNullOrWhiteSpace(query.SortColumn)
            ? null
            : (query.IsSortDescending ? "-" : string.Empty) + query.SortColumn;

    /// <summary>
    /// The only kind of request this resource makes, and a GET: there is no write path here at all.
    /// A non-2xx and a transport failure leave as <see cref="DownstreamCallException"/>, while a
    /// caller's cancellation still propagates, because <see cref="DownstreamCall"/> reports only a
    /// timeout as a downstream failure.
    /// </summary>
    private Task<EmployeeApiEnvelope<T>> GetEnvelopeAsync<T>(
        string relativeUrl,
        string callDescription,
        CancellationToken cancellationToken)
        where T : class
        => DownstreamCall.InvokeAsync(
            async () =>
            {
                var httpClient = httpClientFactory.CreateClient(
                    ServiceDataRegistration.DepartmentRolesHttpClientName);

                using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);

                // Set per request rather than on the client, because the tenant belongs to the caller
                // and the client is shared. Without this header the service answers 401.
                request.Headers.Add(ServiceDataRegistration.TenantIdHeaderName, CurrentTenantId().ToString());

                using var response = await httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<EmployeeApiEnvelope<T>>(
                        ResponseJsonOptions,
                        cancellationToken)
                    ?? new EmployeeApiEnvelope<T>(null, IsSuccessful: false, Errors: null);
            },
            callDescription);

    /// <summary>The action filter rejects a request carrying no tenant before a handler runs, so an
    /// absent one here is a wiring fault rather than a caller's mistake.</summary>
    private Guid CurrentTenantId()
        => currentTenant.TenantId
            ?? throw new InvalidOperationException($"No current tenant is set for the {ResourceKey} resource.");

    private static bool IsRecordAbsent<T>(EmployeeApiEnvelope<T> envelope, Guid id)
        where T : class
        => envelope.Errors?.Any(error =>
            string.Equals(error.Metadata?.ParamName, MissingIdParamName, StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(error.Metadata?.ParamValue, out var missingId)
            && missingId == id) is true;

    /// <summary>The envelope carries no status code, only messages, so they become the inner exception
    /// rather than being dropped.</summary>
    private static DownstreamCallException Failed<T>(EmployeeApiEnvelope<T> envelope, string callDescription)
        where T : class
        => new(
            ServiceDataErrors.DownstreamCallFailed(callDescription, null),
            new InvalidOperationException(
                string.Join(
                    "; ",
                    envelope.Errors?.Select(error => error.Message) ?? ["the response carried no errors"])));

    // Cells are positional: this order is the Descriptor.Columns order. The two ids carry links rather
    // than resolved names: neither is a field of this route, so a name would cost a call per page for
    // a column the link already reaches.
    private static ResourceRow ToRow(DepartmentRole role)
        => new(
            role.Id,
            role.DeletedAt,
            [
                new Cell(role.DepartmentId.ToString(), DepartmentLink(role)),
                new Cell(role.EmployeeId.ToString(), EmployeeLink(role)),
                new Cell(FieldFormat.Format(role.Role), Link: null),
                new Cell(FieldFormat.Format(role.ActiveFrom), Link: null),
                new Cell(FieldFormat.Format(role.ActiveTo), Link: null),
            ]);

    private static ResourceRecord ToRecord(DepartmentRole role)
        => new(
            role.Id,
            Title(role),
            [
                new FieldValue("Id", role.Id.ToString(), FieldKind.Guid, Link: null),
                new FieldValue(
                    "Department id",
                    role.DepartmentId.ToString(),
                    FieldKind.Guid,
                    DepartmentLink(role)),
                new FieldValue("Employee id", role.EmployeeId.ToString(), FieldKind.Guid, EmployeeLink(role)),
                new FieldValue("Role", FieldFormat.Format(role.Role), FieldKind.Enum, Link: null),
                new FieldValue("Active from", FieldFormat.Format(role.ActiveFrom), FieldKind.Date, Link: null),
                new FieldValue("Active to", FieldFormat.Format(role.ActiveTo), FieldKind.Date, Link: null),
                new FieldValue("Deleted at", FieldFormat.Format(role.DeletedAt), FieldKind.DateTime, Link: null),
            ],
            []);

    private static RecordLink DepartmentLink(DepartmentRole role)
        => new(DepartmentsResourceKey, role.DepartmentId);

    private static RecordLink EmployeeLink(DepartmentRole role)
        => new(EmployeesResourceKey, role.EmployeeId);

    /// <summary>No name is reachable without another call, so the title says which role it is and from
    /// when, which is what distinguishes one row of a filtered grid from the next.</summary>
    private static string Title(DepartmentRole role)
        => $"{FieldFormat.Format(role.Role)} from {FieldFormat.Format(role.ActiveFrom)}";
}

/// <summary>
/// The payload of the department roles route, written out here because
/// <c>Numo.Employee.Lib.Models.DepartmentRoleDto</c> is internal. The enum crosses the wire as an
/// integer under the name <c>role</c>, and typing it as the enum is what lets
/// <see cref="FieldFormat"/> render its name.
/// </summary>
internal sealed record DepartmentRole(
    Guid Id,
    Guid DepartmentId,
    Guid EmployeeId,
    [property: JsonPropertyName("role")] DepartmentRoleType Role,
    DateOnly ActiveFrom,
    DateOnly? ActiveTo,
    DateTimeOffset? DeletedAt);

/// <summary>
/// The <c>{value, isSuccessful, errors}</c> shape every Numo service answers with, including for the
/// failures it reports as HTTP 200. The client libraries unwrap this and keep only the messages; this
/// resource keeps <see cref="EmployeeApiErrorMetadata"/>, which is the only exact signal that a record
/// is absent rather than the call having failed.
/// </summary>
internal sealed record EmployeeApiEnvelope<T>(T? Value, bool IsSuccessful, IReadOnlyList<EmployeeApiError>? Errors)
    where T : class;

internal sealed record EmployeeApiError(string? Message, EmployeeApiErrorMetadata? Metadata);

internal sealed record EmployeeApiErrorMetadata(string? Entity, string? ParamName, string? ParamValue);
