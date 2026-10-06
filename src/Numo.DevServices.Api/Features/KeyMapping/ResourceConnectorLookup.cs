namespace Numo.DevServices.Api.Features.KeyMapping;

/// <summary>
/// A resource's connectors with names and key fields. A resource's own connector route carries
/// only connector ids, while the conversions look connectors up by name, so the two lists are joined.
/// Public because a public handler cannot take an internal parameter type (CS0051).
/// </summary>
public sealed class ResourceConnectorLookup(ConfigurationKeysApi api)
{
    public async Task<NumoResult<IReadOnlyList<KeyMappingConnector>>> GetConnectorsAsync(
        Guid clientId,
        Guid resourceId,
        CancellationToken cancellationToken)
    {
        var resourceConnectors = await api.GetAsync<List<ConfigurationResourceConnector>>(
            $"api/clients/{clientId}/resources/{resourceId}/connectors",
            $"the connectors of resource {resourceId}",
            cancellationToken);

        if (resourceConnectors.IsFailed)
        {
            return NumoResult.Fail<IReadOnlyList<KeyMappingConnector>>(resourceConnectors.Errors);
        }

        var connectors = await api.GetAsync<List<ConfigurationConnector>>(
            "api/connectors",
            "the connector list",
            cancellationToken);

        if (connectors.IsFailed)
        {
            return NumoResult.Fail<IReadOnlyList<KeyMappingConnector>>(connectors.Errors);
        }

        var namesById = connectors.Value.ToDictionary(connector => connector.Id, connector => connector.Name);

        IReadOnlyList<KeyMappingConnector> joined = resourceConnectors.Value
            .Select(resourceConnector => new KeyMappingConnector(
                resourceConnector.ConnectorId,
                namesById.GetValueOrDefault(resourceConnector.ConnectorId),
                resourceConnector.KeyFields ?? []))
            .OrderBy(connector => connector.Name)
            .ToList();

        return NumoResult.Ok(joined);
    }

    /// <summary>The service answers an unattached connector, or one without key fields, with an
    /// empty result instead of an error, so it is resolved here before a conversion is sent.</summary>
    public async Task<NumoResult<KeyMappingConnector>> FindAttachedAsync(
        Guid clientId,
        Guid resourceId,
        string connectorName,
        CancellationToken cancellationToken)
    {
        var connectors = await GetConnectorsAsync(clientId, resourceId, cancellationToken);

        if (connectors.IsFailed)
        {
            return NumoResult.Fail<KeyMappingConnector>(connectors.Errors);
        }

        var connector = connectors.Value.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, connectorName, StringComparison.OrdinalIgnoreCase)
            && candidate.KeyFields.Count > 0);

        return connector is null
            ? NumoResult.Fail<KeyMappingConnector>(KeyMappingErrors.ConnectorNotAttached(resourceId, connectorName))
            : NumoResult.Ok(connector);
    }
}
