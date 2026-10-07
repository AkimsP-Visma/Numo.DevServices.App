namespace Numo.DevServices.Api.Features.KeyMapping;

public sealed record GetKeyMappingConnectorsQuery(Guid ClientId, Guid ResourceId);

public sealed class GetKeyMappingConnectorsValidator : AbstractValidator<GetKeyMappingConnectorsQuery>
{
    public GetKeyMappingConnectorsValidator()
    {
        RuleFor(query => query.ClientId).NotEmpty();
        RuleFor(query => query.ResourceId).NotEmpty();
    }
}

public sealed class GetKeyMappingConnectorsHandler(ResourceConnectorLookup connectorLookup)
{
    public Task<NumoResult<IReadOnlyList<KeyMappingConnector>>> HandleAsync(
        GetKeyMappingConnectorsQuery query,
        CancellationToken cancellationToken)
        => connectorLookup.GetConnectorsAsync(query.ClientId, query.ResourceId, cancellationToken);
}
