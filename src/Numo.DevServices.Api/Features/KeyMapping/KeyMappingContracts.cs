namespace Numo.DevServices.Api.Features.KeyMapping;

// The wire contract of the slice. A connector key travels as its values in KeyFields order rather
// than as a dictionary, so no JSON naming policy can rename a key field out from under its value.

public sealed record KeyMappingOption(Guid Id, string Name);

/// <param name="Name">Null when the connector id is absent from the connector list; conversion
/// needs the name, so such a connector cannot be converted against.</param>
public sealed record KeyMappingConnector(Guid ConnectorId, string? Name, IReadOnlyList<string> KeyFields);

/// <param name="ConnectorKeyValues">One value per key field, in KeyFields order; null when unmapped.</param>
/// <param name="NumoKey">Null when unmapped.</param>
public sealed record KeyMappingRow(IReadOnlyList<string>? ConnectorKeyValues, Guid? NumoKey)
{
    public bool IsMapped => ConnectorKeyValues is not null && NumoKey is not null;
}

/// <summary>One row per submitted input row, in submission order, duplicates included.</summary>
public sealed record KeyMappingResult(
    IReadOnlyList<string> KeyFields,
    IReadOnlyList<KeyMappingRow> Rows,
    string? Notice)
{
    /// <summary>The service answers a missing loaded dataset with an all-miss result, not an error,
    /// so an all-miss result must not read as "these keys do not exist".</summary>
    private const string NothingMatchedNotice =
        "No key matched. If you expected matches, check the tenant id, which is sent as the organization id: "
        + "the service finds nothing when no loaded dataset exists for this client, resource, connector and organization.";

    public static KeyMappingResult From(IReadOnlyList<string> keyFields, IReadOnlyList<KeyMappingRow> rows)
    {
        var isNothingMapped = rows.All(row => !row.IsMapped);

        return new KeyMappingResult(keyFields, rows, isNothingMapped ? NothingMatchedNotice : null);
    }
}

/// <summary>Validators of both conversions share it: the service streams the whole loaded dataset
/// per call whatever the batch size, so this bounds the request and the result table, not that.</summary>
internal static class KeyMappingLimits
{
    public const int MaxBatchSize = 1000;
}
