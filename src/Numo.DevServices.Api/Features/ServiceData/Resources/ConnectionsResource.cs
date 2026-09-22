using System.Web;

namespace Numo.DevServices.Api.Features.ServiceData.Resources;

/// <summary>
/// The Configuration API's connections. <c>expand</c> exists as a query parameter on this route in
/// the OpenAPI spec, but it is never sent here, deliberately: the connection response embeds
/// credentials and certificates when expanded, and the settled security decision is that neither
/// ever appears in a list. <see cref="Connection"/> has no property for either, so even an
/// unexpected expansion cannot leak into a cell - not just discipline, a structural guarantee.
/// </summary>
public sealed class ConnectionsResource(DataIntegrationConfigurationApi api) : IServiceDataResource
{
    private const string ResourceKey = "di-connections";
    private const string ConnectorsResourceKey = "di-connectors";
    private const string CredentialsResourceKey = "di-connection-credentials";
    private const string CertificatesResourceKey = "di-connection-certificates";

    private const string OrganizationIdFilterKey = "organizationId";
    private const string ConnectorNameFilterKey = "connectorName";

    // Verified live against test.numo.lv: GET /api/connections with no organizationId at all
    // answers 200 with every connection, so the client library's GetConnectionsByOrganizationId
    // signature (a non-nullable Guid) does not reflect a route requirement - it is just that
    // method's own narrower shape.
    public ResourceDescriptor Descriptor { get; } = new(
        ResourceKey,
        "Connections",
        "Numo.DataIntegration.Configuration.Api",
        ResourceSection.DataIntegration,
        [
            new ColumnDescriptor("name", "Name", FieldKind.Text, IsSortable: false),
            new ColumnDescriptor("connectorId", "Connector id", FieldKind.Guid, IsSortable: false),
            new ColumnDescriptor("organizationId", "Organization id", FieldKind.Guid, IsSortable: false),
        ],
        [
            new FilterDescriptor(OrganizationIdFilterKey, "Organization id", FilterKind.Guid, Options: null),
            new FilterDescriptor(ConnectorNameFilterKey, "Connector name", FilterKind.Text, Options: null),
        ],
        RequiresTenant: false);

    public Task<ResourcePage> GetPageAsync(ResourceQuery query, CancellationToken cancellationToken)
        => ResourcePageBuilder.BuildUnpagedAsync(query, () => FetchAllAsync(query, cancellationToken), ToRow);

    public async Task<ResourceRecord?> GetByIdAsync(
        Guid id,
        IReadOnlyDictionary<string, string> filters,
        CancellationToken cancellationToken)
    {
        var connection = await api.GetOrNullAsync<Connection>(
            $"api/connections/{id}",
            $"{ResourceKey} record",
            cancellationToken);

        return connection is null ? null : ToRecord(connection);
    }

    private async Task<IEnumerable<Connection>> FetchAllAsync(ResourceQuery query, CancellationToken cancellationToken)
    {
        var parameters = new List<string>();
        var organizationId = ResourceQueryFilters.ReadGuid(query, OrganizationIdFilterKey);
        var connectorName = ResourceQueryFilters.ReadText(query, ConnectorNameFilterKey);

        if (organizationId is not null)
        {
            parameters.Add($"organizationId={organizationId}");
        }

        if (connectorName is not null)
        {
            parameters.Add($"connectorName={HttpUtility.UrlEncode(connectorName)}");
        }

        var url = parameters.Count == 0 ? "api/connections" : $"api/connections?{string.Join('&', parameters)}";

        return await api.GetAsync<List<Connection>>(url, $"{ResourceKey} list", cancellationToken);
    }

    private static ResourceRow ToRow(Connection connection)
        => new(
            connection.Id,
            DeletedAt: null,
            [
                new Cell(connection.Name, Link: null),
                new Cell(connection.ConnectorId.ToString(), new RecordLink(ConnectorsResourceKey, connection.ConnectorId)),
                new Cell(connection.OrganizationId?.ToString(), Link: null),
            ]);

    private static ResourceRecord ToRecord(Connection connection)
        => new(
            connection.Id,
            string.IsNullOrWhiteSpace(connection.Name) ? connection.Id.ToString() : connection.Name,
            [
                new FieldValue("Id", connection.Id.ToString(), FieldKind.Guid, Link: null),
                new FieldValue("Name", connection.Name, FieldKind.Text, Link: null),
                new FieldValue(
                    "Connector id",
                    connection.ConnectorId.ToString(),
                    FieldKind.Guid,
                    new RecordLink(ConnectorsResourceKey, connection.ConnectorId)),
                new FieldValue("Organization id", connection.OrganizationId?.ToString(), FieldKind.Guid, Link: null),
            ],
            [
                RelationDescriptor.To("Credentials", CredentialsResourceKey, "connectionId", connection.Id.ToString()),
                RelationDescriptor.To("Certificates", CertificatesResourceKey, "connectionId", connection.Id.ToString()),
            ]);
}

/// <summary>Deliberately missing Credentials, Certificates and Parameters even though the downstream
/// DTO can carry them: see the class-level remark on <see cref="ConnectionsResource"/>.</summary>
internal sealed record Connection(Guid Id, string? Name, Guid ConnectorId, Guid? OrganizationId);
