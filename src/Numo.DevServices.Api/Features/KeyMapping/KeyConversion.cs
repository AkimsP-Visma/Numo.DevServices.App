namespace Numo.DevServices.Api.Features.KeyMapping;

/// <summary>What both conversion handlers share: the fixed downstream routes, and moving a connector
/// key between this slice's values-in-key-field-order form and the service's dictionary form.</summary>
internal static class KeyConversion
{
    public const string NumoKeysRoute = "numo-keys";
    public const string ConnectorKeysRoute = "connector-keys";

    // A separator no key value is expected to contain, so joined values identify one key.
    private const char IdentitySeparator = '\u001F';

    public static string BuildUrl(
        string route,
        Guid clientId,
        Guid resourceId,
        string connectorName,
        Guid organizationId)
        => $"api/clients/{clientId}/resources/{resourceId}/{route}"
            + $"?connectorName={Uri.EscapeDataString(connectorName)}&organizationId={organizationId}";

    public static Dictionary<string, string> ToDictionary(IReadOnlyList<string> keyFields, IReadOnlyList<string> values)
        => keyFields.Select((keyField, index) => (keyField, value: values[index]))
            .ToDictionary(pair => pair.keyField, pair => pair.value);

    /// <summary>Field names are matched ignoring case, in case the service's serializer renames
    /// dictionary keys. Null when any key field is absent, since a partial key identifies nothing.</summary>
    public static IReadOnlyList<string>? ToValues(IReadOnlyList<string> keyFields, IReadOnlyDictionary<string, string>? connectorKey)
    {
        if (connectorKey is null)
        {
            return null;
        }

        var byField = new Dictionary<string, string>(connectorKey, StringComparer.OrdinalIgnoreCase);
        var values = new List<string>(keyFields.Count);

        foreach (var keyField in keyFields)
        {
            if (!byField.TryGetValue(keyField, out var value))
            {
                return null;
            }

            values.Add(value);
        }

        return values;
    }

    public static string Identity(IEnumerable<string> values) => string.Join(IdentitySeparator, values);

    /// <summary>The 1-based number of the first row lacking a non-blank value for every key field.</summary>
    public static int? FindIncompleteRowNumber(IReadOnlyList<IReadOnlyList<string>> rows, int keyFieldCount)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];

            if (row.Count != keyFieldCount || row.Any(string.IsNullOrWhiteSpace))
            {
                return index + 1;
            }
        }

        return null;
    }
}
