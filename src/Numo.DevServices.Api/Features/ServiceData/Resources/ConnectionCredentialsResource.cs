using System.Security.Cryptography;
using System.Text;

namespace Numo.DevServices.Api.Features.ServiceData.Resources;

/// <summary>
/// A connection's credentials, over <c>/api/connections/{connectionId}/credentials</c>. Security
/// decision, settled: credentials are shown, but never in a list and only from a specific
/// connection record. <see cref="ResourceDescriptor.IsReachableOnlyByRelation"/> keeps this out of
/// the picker, so the only way in is the "Credentials" relation on a connection record, and the
/// call is made only when that button is pressed - nothing here is fetched speculatively.
///
/// The route answers a flat <c>{key: value}</c> dictionary, not a list of rows with ids, so each
/// entry becomes one row keyed by a deterministic hash of its credential name: there is no route
/// to fetch one credential alone, and the hash exists only so "Open" on a row still works rather
/// than pointing at nothing.
/// </summary>
public sealed class ConnectionCredentialsResource(DataIntegrationConfigurationApi api) : IServiceDataResource
{
    private const string ResourceKey = "di-connection-credentials";
    private const string ConnectionIdFilterKey = "connectionId";

    public ResourceDescriptor Descriptor { get; } = new(
        ResourceKey,
        "Connection credentials",
        "Numo.DataIntegration.Configuration.Api",
        ResourceSection.DataIntegration,
        [
            new ColumnDescriptor("key", "Key", FieldKind.Text, IsSortable: false),
            new ColumnDescriptor("value", "Value", FieldKind.Text, IsSortable: false),
        ],
        [
            new FilterDescriptor(ConnectionIdFilterKey, "Connection id", FilterKind.Guid, Options: null, IsRequired: true),
        ],
        RequiresTenant: false,
        IsReachableOnlyByRelation: true);

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
        => await api.GetAsync<Dictionary<string, string>>(
            $"api/connections/{connectionId}/credentials",
            $"{ResourceKey} list",
            cancellationToken);

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
