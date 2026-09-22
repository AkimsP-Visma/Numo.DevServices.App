namespace Numo.DevServices.Api.Features.ServiceData.Resources;

/// <summary>The Configuration API's own <c>/api/connectors</c> - distinct from the Connectors API,
/// which is not used anywhere in this section (see <see cref="DataIntegrationConfigurationApi"/>).
/// No query parameters exist on this route at all.</summary>
public sealed class ConnectorsResource(DataIntegrationConfigurationApi api) : IServiceDataResource
{
    private const string ResourceKey = "di-connectors";

    public ResourceDescriptor Descriptor { get; } = new(
        ResourceKey,
        "Connectors",
        "Numo.DataIntegration.Configuration.Api",
        ResourceSection.DataIntegration,
        [
            new ColumnDescriptor("name", "Name", FieldKind.Text, IsSortable: false),
            new ColumnDescriptor("isDeleted", "Deleted", FieldKind.Boolean, IsSortable: false),
        ],
        [],
        RequiresTenant: false);

    public Task<ResourcePage> GetPageAsync(ResourceQuery query, CancellationToken cancellationToken)
        => ResourcePageBuilder.BuildUnpagedAsync(query, () => FetchAllAsync(cancellationToken), ToRow);

    private async Task<IEnumerable<Connector>> FetchAllAsync(CancellationToken cancellationToken)
        => await api.GetAsync<List<Connector>>("api/connectors", $"{ResourceKey} list", cancellationToken);

    public async Task<ResourceRecord?> GetByIdAsync(
        Guid id,
        IReadOnlyDictionary<string, string> filters,
        CancellationToken cancellationToken)
    {
        var connector = await api.GetOrNullAsync<Connector>(
            $"api/connectors/{id}",
            $"{ResourceKey} record",
            cancellationToken);

        return connector is null ? null : ToRecord(connector);
    }

    private static ResourceRow ToRow(Connector connector)
        => new(
            connector.Id,
            DeletedAt: null,
            [
                new Cell(connector.Name, Link: null),
                new Cell(FieldFormat.Format(connector.IsDeleted), Link: null),
            ]);

    private static ResourceRecord ToRecord(Connector connector)
        => new(
            connector.Id,
            string.IsNullOrWhiteSpace(connector.Name) ? connector.Id.ToString() : connector.Name,
            [
                new FieldValue("Id", connector.Id.ToString(), FieldKind.Guid, Link: null),
                new FieldValue("Name", connector.Name, FieldKind.Text, Link: null),
                new FieldValue("Deleted", FieldFormat.Format(connector.IsDeleted), FieldKind.Boolean, Link: null),
            ],
            []);
}

internal sealed record Connector(Guid Id, string? Name, bool IsDeleted);
