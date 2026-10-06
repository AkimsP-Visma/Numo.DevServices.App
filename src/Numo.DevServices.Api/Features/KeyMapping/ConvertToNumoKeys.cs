namespace Numo.DevServices.Api.Features.KeyMapping;

/// <param name="ConnectorKeys">Each key as its values in the connector's key-field order.</param>
public sealed record ConvertToNumoKeysQuery(
    Guid ClientId,
    Guid ResourceId,
    string ConnectorName,
    Guid OrganizationId,
    IReadOnlyList<IReadOnlyList<string>> ConnectorKeys);

public sealed class ConvertToNumoKeysValidator : AbstractValidator<ConvertToNumoKeysQuery>
{
    public ConvertToNumoKeysValidator()
    {
        RuleFor(query => query.ClientId).NotEmpty();
        RuleFor(query => query.ResourceId).NotEmpty();
        RuleFor(query => query.ConnectorName).NotEmpty();

        // Omitted, the service binds an empty GUID and silently matches nothing.
        RuleFor(query => query.OrganizationId).NotEmpty();

        RuleFor(query => query.ConnectorKeys)
            .NotEmpty()
            .Must(keys => keys.Count <= KeyMappingLimits.MaxBatchSize)
            .WithMessage($"At most {KeyMappingLimits.MaxBatchSize} connector keys can be converted at once.");

        RuleForEach(query => query.ConnectorKeys).NotNull();
    }
}

public sealed class ConvertToNumoKeysHandler(ResourceConnectorLookup connectorLookup, ConfigurationKeysApi api)
{
    public async Task<NumoResult<KeyMappingResult>> HandleAsync(
        ConvertToNumoKeysQuery query,
        CancellationToken cancellationToken)
    {
        var connector = await connectorLookup.FindAttachedAsync(
            query.ClientId, query.ResourceId, query.ConnectorName, cancellationToken);

        if (connector.IsFailed)
        {
            return NumoResult.Fail<KeyMappingResult>(connector.Errors);
        }

        var keyFields = connector.Value.KeyFields;

        // The service answers any incomplete key by returning nothing for the whole batch.
        if (KeyConversion.FindIncompleteRowNumber(query.ConnectorKeys, keyFields.Count) is { } rowNumber)
        {
            return NumoResult.Fail<KeyMappingResult>(KeyMappingErrors.KeyFieldValueMissing(rowNumber, keyFields));
        }

        var mappings = await api.PostAsync<IEnumerable<ConfigurationConnectorKey>, List<ConfigurationNumoKeyMapping>>(
            KeyConversion.BuildUrl(
                KeyConversion.NumoKeysRoute,
                query.ClientId,
                query.ResourceId,
                connector.Value.Name!,
                query.OrganizationId),
            query.ConnectorKeys.Select(values => new ConfigurationConnectorKey(KeyConversion.ToDictionary(keyFields, values))),
            $"numo keys of resource {query.ResourceId}",
            cancellationToken);

        if (mappings.IsFailed)
        {
            return NumoResult.Fail<KeyMappingResult>(mappings.Errors);
        }

        var numoKeysByIdentity = IndexNumoKeys(keyFields, mappings.Value);

        IReadOnlyList<KeyMappingRow> rows = query.ConnectorKeys
            .Select(values => new KeyMappingRow(
                values,
                numoKeysByIdentity.GetValueOrDefault(KeyConversion.Identity(values))))
            .ToList();

        return NumoResult.Ok(KeyMappingResult.From(keyFields, rows));
    }

    /// <summary>The service deduplicates its input, so results are matched back to input rows by
    /// key content rather than position.</summary>
    private static Dictionary<string, Guid?> IndexNumoKeys(
        IReadOnlyList<string> keyFields,
        IEnumerable<ConfigurationNumoKeyMapping> mappings)
    {
        var index = new Dictionary<string, Guid?>();

        foreach (var mapping in mappings)
        {
            var values = KeyConversion.ToValues(keyFields, mapping.ConnectorKey?.Value);

            if (values is not null && mapping.NumoKey is not null)
            {
                index.TryAdd(KeyConversion.Identity(values), mapping.NumoKey);
            }
        }

        return index;
    }
}
