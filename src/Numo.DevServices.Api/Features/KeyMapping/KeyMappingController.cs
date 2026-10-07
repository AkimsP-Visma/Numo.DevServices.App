namespace Numo.DevServices.Api.Features.KeyMapping;

public sealed record ConvertToNumoKeysRequest(
    string ConnectorName,
    Guid OrganizationId,
    IReadOnlyList<IReadOnlyList<string>> ConnectorKeys);

public sealed record ConvertToConnectorKeysRequest(
    string ConnectorName,
    Guid OrganizationId,
    IReadOnlyList<Guid> NumoKeys);

/// <summary>Conversions are POSTs only because a batch of keys does not fit a query string; both
/// downstream routes are read-only.</summary>
[ApiController]
[Route("api/key-mappings/clients")]
public sealed class KeyMappingController(INumoMediator mediator) : ControllerBase
{
    [HttpGet]
    public Task<NumoResult<IReadOnlyList<KeyMappingOption>>> GetClients(CancellationToken cancellationToken)
        => mediator.SendAsync<IReadOnlyList<KeyMappingOption>>(new GetKeyMappingClientsQuery(), cancellationToken);

    [HttpGet("{clientId:guid}/resources")]
    public Task<NumoResult<IReadOnlyList<KeyMappingOption>>> GetResources(
        Guid clientId,
        CancellationToken cancellationToken)
        => mediator.SendAsync<IReadOnlyList<KeyMappingOption>>(
            new GetKeyMappingResourcesQuery(clientId),
            cancellationToken);

    [HttpGet("{clientId:guid}/resources/{resourceId:guid}/connectors")]
    public Task<NumoResult<IReadOnlyList<KeyMappingConnector>>> GetConnectors(
        Guid clientId,
        Guid resourceId,
        CancellationToken cancellationToken)
        => mediator.SendAsync<IReadOnlyList<KeyMappingConnector>>(
            new GetKeyMappingConnectorsQuery(clientId, resourceId),
            cancellationToken);

    [HttpPost("{clientId:guid}/resources/{resourceId:guid}/numo-keys")]
    public Task<NumoResult<KeyMappingResult>> ConvertToNumoKeys(
        Guid clientId,
        Guid resourceId,
        [FromBody] ConvertToNumoKeysRequest request,
        CancellationToken cancellationToken)
        => mediator.SendAsync<KeyMappingResult>(
            new ConvertToNumoKeysQuery(
                clientId,
                resourceId,
                request.ConnectorName,
                request.OrganizationId,
                request.ConnectorKeys),
            cancellationToken);

    [HttpPost("{clientId:guid}/resources/{resourceId:guid}/connector-keys")]
    public Task<NumoResult<KeyMappingResult>> ConvertToConnectorKeys(
        Guid clientId,
        Guid resourceId,
        [FromBody] ConvertToConnectorKeysRequest request,
        CancellationToken cancellationToken)
        => mediator.SendAsync<KeyMappingResult>(
            new ConvertToConnectorKeysQuery(
                clientId,
                resourceId,
                request.ConnectorName,
                request.OrganizationId,
                request.NumoKeys),
            cancellationToken);
}
