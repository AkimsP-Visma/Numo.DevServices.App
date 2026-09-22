using System.Web;

namespace Numo.DevServices.Api.Features.ServiceData.Resources;

/// <summary>
/// The Configuration API's clients - services registered with DataIntegration, such as
/// Numo.Employee.Api. Unpaged: see <see cref="ResourcePageBuilder.BuildUnpagedAsync{TItem}"/> for
/// why that is honest here and would not be for Person/Employee data.
/// </summary>
public sealed class ClientsResource(DataIntegrationConfigurationApi api) : IServiceDataResource
{
    private const string ResourceKey = "di-clients";
    private const string ClientResourcesResourceKey = "di-client-resources";
    private const string NameFilterKey = "name";

    // No route in this section supports OrderBy at all: it is absent from every list route's query
    // parameters in the saved OpenAPI spec, unlike the Person/Employee services.
    public ResourceDescriptor Descriptor { get; } = new(
        ResourceKey,
        "Clients",
        "Numo.DataIntegration.Configuration.Api",
        ResourceSection.DataIntegration,
        [
            new ColumnDescriptor("name", "Name", FieldKind.Text, IsSortable: false),
        ],
        [
            new FilterDescriptor(NameFilterKey, "Name", FilterKind.Text, Options: null),
        ],
        RequiresTenant: false);

    public Task<ResourcePage> GetPageAsync(ResourceQuery query, CancellationToken cancellationToken)
        => ResourcePageBuilder.BuildUnpagedAsync(query, () => FetchAllAsync(query, cancellationToken), ToRow);

    public async Task<ResourceRecord?> GetByIdAsync(
        Guid id,
        IReadOnlyDictionary<string, string> filters,
        CancellationToken cancellationToken)
    {
        var client = await api.GetOrNullAsync<Client>(
            $"api/clients/{id}",
            $"{ResourceKey} record",
            cancellationToken);

        return client is null ? null : ToRecord(client);
    }

    private async Task<IEnumerable<Client>> FetchAllAsync(ResourceQuery query, CancellationToken cancellationToken)
    {
        var name = ResourceQueryFilters.ReadText(query, NameFilterKey);
        var url = name is null ? "api/clients" : $"api/clients?name={HttpUtility.UrlEncode(name)}";

        return await api.GetAsync<List<Client>>(url, $"{ResourceKey} list", cancellationToken);
    }

    private static ResourceRow ToRow(Client client)
        => new(client.Id, DeletedAt: null, [new Cell(client.Name, Link: null)]);

    private static ResourceRecord ToRecord(Client client)
        => new(
            client.Id,
            string.IsNullOrWhiteSpace(client.Name) ? client.Id.ToString() : client.Name,
            [
                new FieldValue("Id", client.Id.ToString(), FieldKind.Guid, Link: null),
                new FieldValue("Name", client.Name, FieldKind.Text, Link: null),
            ],
            [
                RelationDescriptor.To("Resources", ClientResourcesResourceKey, "clientId", client.Id.ToString()),
            ]);
}

/// <summary>The Configuration API's own wire shape for one client, verified live: a bare object,
/// no envelope.</summary>
internal sealed record Client(Guid Id, string? Name);
