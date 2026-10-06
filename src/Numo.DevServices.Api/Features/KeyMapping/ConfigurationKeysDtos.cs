namespace Numo.DevServices.Api.Features.KeyMapping;

// The Configuration API's own shapes, read only as far as this slice needs them.

internal sealed record ConfigurationClient(Guid Id, string? Name);

internal sealed record ConfigurationResource(Guid Id, string? Name);

internal sealed record ConfigurationResourceConnector(Guid ConnectorId, IReadOnlyList<string>? KeyFields);

internal sealed record ConfigurationConnector(Guid Id, string? Name);

/// <summary>The service's <c>ResourceConnectorKeyDto</c>: one value per key field.</summary>
internal sealed record ConfigurationConnectorKey(IReadOnlyDictionary<string, string>? Value);

internal sealed record ConfigurationNumoKeyMapping(ConfigurationConnectorKey? ConnectorKey, Guid? NumoKey);

internal sealed record ConfigurationConnectorKeysMapping(Guid NumoKey, IReadOnlyDictionary<string, string>? ConnectorKeys);
