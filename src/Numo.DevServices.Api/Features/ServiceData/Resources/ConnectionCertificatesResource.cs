namespace Numo.DevServices.Api.Features.ServiceData.Resources;

/// <summary>
/// A connection's certificates. Security decision, settled: exactly the same treatment as
/// credentials - never in a list, only from a specific connection record, over the nested route
/// only. <see cref="ResourceDescriptor.IsReachableOnlyByRelation"/> keeps this out of the picker.
///
/// There is no nested list route for this (only the flat, forbidden
/// <c>/api/connections/certificates</c>, which would expose every connection's certificates at
/// once, and the nested single-certificate route
/// <c>/api/connections/{connectionId}/certificates/{certificateId}</c>, which needs an id we do not
/// have yet when listing). So the list is read from the connection's own detail route with
/// <c>expand</c> set to include its embedded certificates array.
///
/// Verified live against test.numo.lv: <c>expand=certificates</c> (and the comma-joined
/// <c>expand=credentials,certificates</c>) does add a <c>certificates</c> array to the response,
/// confirming the mechanism - though no connection in the test tenant actually carries a
/// certificate, so the array's own field shape (<see cref="ConnectionCertificate"/>) is inferred
/// from the OpenAPI schema, not observed with real data.
/// </summary>
public sealed class ConnectionCertificatesResource(DataIntegrationConfigurationApi api) : IServiceDataResource
{
    private const string ResourceKey = "di-connection-certificates";
    private const string ConnectionIdFilterKey = "connectionId";

    // Unverified live: see the class-level remark.
    private const string ExpandCertificatesValue = "certificates";

    public ResourceDescriptor Descriptor { get; } = new(
        ResourceKey,
        "Connection certificates",
        "Numo.DataIntegration.Configuration.Api",
        ResourceSection.DataIntegration,
        [
            new ColumnDescriptor("alias", "Alias", FieldKind.Text, IsSortable: false),
            new ColumnDescriptor("subject", "Subject", FieldKind.Text, IsSortable: false),
            new ColumnDescriptor("issuer", "Issuer", FieldKind.Text, IsSortable: false),
            new ColumnDescriptor("notBefore", "Not before", FieldKind.DateTime, IsSortable: false),
            new ColumnDescriptor("notAfter", "Not after", FieldKind.DateTime, IsSortable: false),
        ],
        [
            new FilterDescriptor(ConnectionIdFilterKey, "Connection id", FilterKind.Guid, Options: null, IsRequired: true),
        ],
        RequiresTenant: false,
        IsReachableOnlyByRelation: true);

    public Task<ResourcePage> GetPageAsync(ResourceQuery query, CancellationToken cancellationToken)
        => ResourcePageBuilder.BuildUnpagedAsync(
            query,
            () => FetchCertificatesAsync(RequireConnectionId(query.Filters), cancellationToken),
            ToRow);

    public async Task<ResourceRecord?> GetByIdAsync(
        Guid id,
        IReadOnlyDictionary<string, string> filters,
        CancellationToken cancellationToken)
    {
        var connectionId = RequireConnectionId(filters);

        var certificate = await api.GetOrNullAsync<ConnectionCertificate>(
            $"api/connections/{connectionId}/certificates/{id}",
            $"{ResourceKey} record",
            cancellationToken);

        return certificate is null ? null : ToRecord(certificate);
    }

    private async Task<IEnumerable<ConnectionCertificate>> FetchCertificatesAsync(
        Guid connectionId,
        CancellationToken cancellationToken)
    {
        var connection = await api.GetOrNullAsync<ConnectionWithCertificates>(
            $"api/connections/{connectionId}?expand={ExpandCertificatesValue}",
            $"{ResourceKey} list",
            cancellationToken);

        return connection?.Certificates ?? [];
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

    private static ResourceRow ToRow(ConnectionCertificate certificate)
        => new(
            certificate.Id,
            DeletedAt: null,
            [
                new Cell(certificate.Alias, Link: null),
                new Cell(certificate.Subject, Link: null),
                new Cell(certificate.Issuer, Link: null),
                new Cell(FieldFormat.Format(certificate.NotBefore), Link: null),
                new Cell(FieldFormat.Format(certificate.NotAfter), Link: null),
            ]);

    private static ResourceRecord ToRecord(ConnectionCertificate certificate)
        => new(
            certificate.Id,
            certificate.Alias ?? certificate.Id.ToString(),
            [
                new FieldValue("Id", certificate.Id.ToString(), FieldKind.Guid, Link: null),
                new FieldValue("Alias", certificate.Alias, FieldKind.Text, Link: null),
                new FieldValue("Subject", certificate.Subject, FieldKind.Text, Link: null),
                new FieldValue("Issuer", certificate.Issuer, FieldKind.Text, Link: null),
                new FieldValue("Thumbprint", certificate.Thumbprint, FieldKind.Text, Link: null),
                new FieldValue("Key algorithm", certificate.KeyAlgorithm, FieldKind.Text, Link: null),
                new FieldValue("Key size", FieldFormat.FormatNumber(certificate.KeySize), FieldKind.Number, Link: null),
                new FieldValue("Not before", FieldFormat.Format(certificate.NotBefore), FieldKind.DateTime, Link: null),
                new FieldValue("Not after", FieldFormat.Format(certificate.NotAfter), FieldKind.DateTime, Link: null),
            ],
            []);
}

internal sealed record ConnectionCertificate(
    Guid Id,
    string? Alias,
    string? Subject,
    string? Issuer,
    string? Thumbprint,
    string? KeyAlgorithm,
    int KeySize,
    DateTimeOffset NotBefore,
    DateTimeOffset NotAfter);

/// <summary>Only what this resource needs from the connection's own shape - see the class-level
/// remark on the unverified expand value.</summary>
internal sealed record ConnectionWithCertificates(IReadOnlyList<ConnectionCertificate>? Certificates);
