namespace Numo.DevServices.Api.Features.ServiceData.Resources;

/// <summary>
/// A client's resources, nested under <c>/api/clients/{clientId}/resources</c>. Reachable only by
/// following the "Resources" relation from a <see cref="ClientsResource"/> record: the required
/// <c>clientId</c> filter is what a plain browse-everything picker cannot supply.
///
/// Its connector-key and numo-key conversions need a connector and an organization id that a
/// record does not carry, so a record links to the Key Mapping page instead of showing them.
/// </summary>
public sealed class ClientResourcesResource(DataIntegrationConfigurationApi api) : IServiceDataResource
{
    private const string ResourceKey = "di-client-resources";
    private const string ClientsResourceKey = "di-clients";
    private const string ClientIdFilterKey = "clientId";
    private const string KeyMappingRoute = "/key-mapping";
    private const string KeyMappingClientIdParameter = "clientId";
    private const string KeyMappingResourceIdParameter = "resourceId";

    public ResourceDescriptor Descriptor { get; } = new(
        ResourceKey,
        "Client resources",
        "Numo.DataIntegration.Configuration.Api",
        ResourceSection.DataIntegration,
        [
            new ColumnDescriptor("name", "Name", FieldKind.Text, IsSortable: false),
            new ColumnDescriptor("clientId", "Client id", FieldKind.Guid, IsSortable: false),
        ],
        [
            new FilterDescriptor(ClientIdFilterKey, "Client id", FilterKind.Guid, Options: null, IsRequired: true),
        ],
        RequiresTenant: false);

    public Task<ResourcePage> GetPageAsync(ResourceQuery query, CancellationToken cancellationToken)
        => ResourcePageBuilder.BuildUnpagedAsync(
            query,
            () => FetchAllAsync(RequireClientId(query.Filters), cancellationToken),
            ToRow);

    private async Task<IEnumerable<ClientResource>> FetchAllAsync(Guid clientId, CancellationToken cancellationToken)
        => await api.GetAsync<List<ClientResource>>(
            $"api/clients/{clientId}/resources",
            $"{ResourceKey} list",
            cancellationToken);

    public async Task<ResourceRecord?> GetByIdAsync(
        Guid id,
        IReadOnlyDictionary<string, string> filters,
        CancellationToken cancellationToken)
    {
        var clientId = RequireClientId(filters);

        var resource = await api.GetOrNullAsync<ClientResource>(
            $"api/clients/{clientId}/resources/{id}",
            $"{ResourceKey} record",
            cancellationToken);

        return resource is null ? null : ToRecord(resource);
    }

    /// <summary>The page validator already rejects a request missing this before GetPageAsync
    /// runs; GetByIdAsync's caller does not, so this reports the same failure cleanly rather than
    /// throwing a bare exception that would surface as an unhandled 500.</summary>
    private static Guid RequireClientId(IReadOnlyDictionary<string, string> filters)
        => filters.TryGetValue(ClientIdFilterKey, out var value) && Guid.TryParse(value, out var clientId)
            ? clientId
            : throw new DownstreamCallException(
                ServiceDataErrors.RequiredFilterMissing(ResourceKey, ClientIdFilterKey),
                new InvalidOperationException($"{ResourceKey} was reached with no valid clientId filter."));

    private static ResourceRow ToRow(ClientResource resource)
        => new(
            resource.Id,
            DeletedAt: null,
            [
                new Cell(resource.Name, Link: null),
                new Cell(resource.ClientId.ToString(), new RecordLink(ClientsResourceKey, resource.ClientId)),
            ]);

    private static ResourceRecord ToRecord(ClientResource resource)
        => new(
            resource.Id,
            string.IsNullOrWhiteSpace(resource.Name) ? resource.Id.ToString() : resource.Name,
            [
                new FieldValue("Id", resource.Id.ToString(), FieldKind.Guid, Link: null),
                new FieldValue("Name", resource.Name, FieldKind.Text, Link: null),
                new FieldValue(
                    "Client id",
                    resource.ClientId.ToString(),
                    FieldKind.Guid,
                    new RecordLink(ClientsResourceKey, resource.ClientId)),
            ],
            [],
            [
                new ToolLink(
                    "Convert keys",
                    KeyMappingRoute,
                    new Dictionary<string, string>
                    {
                        [KeyMappingClientIdParameter] = resource.ClientId.ToString(),
                        [KeyMappingResourceIdParameter] = resource.Id.ToString(),
                    }),
            ]);
}

internal sealed record ClientResource(Guid Id, Guid ClientId, string? Name);
