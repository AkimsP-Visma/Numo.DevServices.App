using System.Security.Cryptography;
using System.Text;

namespace Numo.DevServices.Api.Features.ServiceData.Resources;

/// <summary>
/// A connection's parameters, over the same detail route as certificates:
/// <c>/api/connections/{connectionId}?expand=parameters</c>. Unlike credentials and certificates,
/// parameters are not secret - the response embeds them as a flat <c>{key: value}</c> dictionary of
/// plain configuration (verified live against test.numo.lv: every connection probed carries a
/// RestUrl, not a secret), so this resource is an ordinary relation rather than
/// <see cref="ResourceDescriptor.IsReachableOnlyByRelation"/>.
///
/// The route answers a dictionary, not a list of rows with ids, so each entry becomes one row keyed
/// by a deterministic hash of its parameter name - the same synthetic-id device
/// <see cref="ConnectionCredentialsResource"/> uses, for the same reason: there is no route to fetch
/// one parameter alone.
/// </summary>
public sealed class ConnectionParametersResource(DataIntegrationConfigurationApi api) : IServiceDataResource
{
    private const string ResourceKey = "di-connection-parameters";
    private const string ConnectionIdFilterKey = "connectionId";
    private const string ExpandParametersValue = "parameters";

    public ResourceDescriptor Descriptor { get; } = new(
        ResourceKey,
        "Connection parameters",
        "Numo.DataIntegration.Configuration.Api",
        ResourceSection.DataIntegration,
        [
            new ColumnDescriptor("key", "Key", FieldKind.Text, IsSortable: false),
            new ColumnDescriptor("value", "Value", FieldKind.Text, IsSortable: false),
        ],
        [
            new FilterDescriptor(ConnectionIdFilterKey, "Connection id", FilterKind.Guid, Options: null, IsRequired: true),
        ],
        RequiresTenant: false);

    public Task<ResourcePage> GetPageAsync(ResourceQuery query, CancellationToken cancellationToken)
        => ResourcePageBuilder.BuildUnpagedAsync(
            query,
            () => FetchEntriesAsync(RequireConnectionId(query.Filters), cancellationToken),
            ToRow);

    public async Task<ResourceRecord?> GetByIdAsync(
        Guid id,
        IReadOnlyDictionary<string, string> filters,
        CancellationToken cancellationToken)
    {
        var entries = await FetchEntriesAsync(RequireConnectionId(filters), cancellationToken);
        var entry = entries.FirstOrDefault(candidate => SyntheticId(candidate.Key) == id);

        return entry.Key is null ? null : ToRecord(entry);
    }

    private async Task<IEnumerable<KeyValuePair<string, string>>> FetchEntriesAsync(
        Guid connectionId,
        CancellationToken cancellationToken)
    {
        var connection = await api.GetOrNullAsync<ConnectionWithParameters>(
            $"api/connections/{connectionId}?expand={ExpandParametersValue}",
            $"{ResourceKey} list",
            cancellationToken);

        return connection?.Parameters ?? new Dictionary<string, string>();
    }

    /// <summary>The page validator already rejects a request missing this before GetPageAsync
    /// runs; GetByIdAsync's caller does not, so this reports the same failure cleanly rather than
    /// throwing a bare exception that would surface as an unhandled 500.</summary>
    private static Guid RequireConnectionId(IReadOnlyDictionary<string, string> filters)
        => filters.TryGetValue(ConnectionIdFilterKey, out var value) && Guid.TryParse(value, out var connectionId)
            ? connectionId
            : throw new DownstreamCallException(
                ServiceDataErrors.RequiredFilterMissing(ResourceKey, ConnectionIdFilterKey),
                new InvalidOperationException($"{ResourceKey} was reached with no valid connectionId filter."));

    /// <summary>Stable across the list and the by-id fetch of the same connection, and nothing
    /// else needs it to mean anything.</summary>
    private static Guid SyntheticId(string key)
        => new(MD5.HashData(Encoding.UTF8.GetBytes(key)));

    private static ResourceRow ToRow(KeyValuePair<string, string> entry)
        => new(
            SyntheticId(entry.Key),
            DeletedAt: null,
            [
                new Cell(entry.Key, Link: null),
                new Cell(entry.Value, Link: null),
            ]);

    private static ResourceRecord ToRecord(KeyValuePair<string, string> entry)
        => new(
            SyntheticId(entry.Key),
            entry.Key,
            [
                new FieldValue("Key", entry.Key, FieldKind.Text, Link: null),
                new FieldValue("Value", entry.Value, FieldKind.Text, Link: null),
            ],
            []);
}

/// <summary>Only what this resource needs from the connection's own shape.</summary>
internal sealed record ConnectionWithParameters(IReadOnlyDictionary<string, string>? Parameters);
