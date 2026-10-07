namespace Numo.DevServices.Api.Features.KeyMapping;

public sealed record ConvertToConnectorKeysQuery(
    Guid ClientId,
    Guid ResourceId,
    string ConnectorName,
    Guid OrganizationId,
    IReadOnlyList<Guid> NumoKeys);

public sealed class ConvertToConnectorKeysValidator : AbstractValidator<ConvertToConnectorKeysQuery>
{
    public ConvertToConnectorKeysValidator()
    {
        RuleFor(query => query.ClientId).NotEmpty();
        RuleFor(query => query.ResourceId).NotEmpty();
        RuleFor(query => query.ConnectorName).NotEmpty();

        // Omitted, the service binds an empty GUID and silently matches nothing.
        RuleFor(query => query.OrganizationId).NotEmpty();

        RuleFor(query => query.NumoKeys)
            .NotEmpty()
            .Must(keys => keys.Count <= KeyMappingLimits.MaxBatchSize)
            .WithMessage($"At most {KeyMappingLimits.MaxBatchSize} numo keys can be converted at once.");

        RuleForEach(query => query.NumoKeys).NotEmpty();
    }
}

public sealed class ConvertToConnectorKeysHandler(ResourceConnectorLookup connectorLookup, ConfigurationKeysApi api)
{
    public async Task<NumoResult<KeyMappingResult>> HandleAsync(
        ConvertToConnectorKeysQuery query,
        CancellationToken cancellationToken)
    {
        var connector = await connectorLookup.FindAttachedAsync(
            query.ClientId, query.ResourceId, query.ConnectorName, cancellationToken);

        if (connector.IsFailed)
        {
            return NumoResult.Fail<KeyMappingResult>(connector.Errors);
        }

        var keyFields = connector.Value.KeyFields;

        var mappings = await api.PostAsync<IEnumerable<Guid>, List<ConfigurationConnectorKeysMapping>>(
            KeyConversion.BuildUrl(
                KeyConversion.ConnectorKeysRoute,
                query.ClientId,
                query.ResourceId,
                connector.Value.Name!,
                query.OrganizationId),
            query.NumoKeys,
            $"connector keys of resource {query.ResourceId}",
            cancellationToken);

        if (mappings.IsFailed)
        {
            return NumoResult.Fail<KeyMappingResult>(mappings.Errors);
        }

        // The service deduplicates its input, so results are matched back to input rows by numo key.
        var connectorKeysByNumoKey = new Dictionary<Guid, IReadOnlyList<string>?>();

        foreach (var mapping in mappings.Value)
        {
            connectorKeysByNumoKey.TryAdd(mapping.NumoKey, KeyConversion.ToValues(keyFields, mapping.ConnectorKeys));
        }

        IReadOnlyList<KeyMappingRow> rows = query.NumoKeys
            .Select(numoKey => new KeyMappingRow(connectorKeysByNumoKey.GetValueOrDefault(numoKey), numoKey))
            .ToList();

        return NumoResult.Ok(KeyMappingResult.From(keyFields, rows));
    }
}
