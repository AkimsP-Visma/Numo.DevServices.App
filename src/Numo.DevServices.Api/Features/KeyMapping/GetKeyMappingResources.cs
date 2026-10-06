namespace Numo.DevServices.Api.Features.KeyMapping;

public sealed record GetKeyMappingResourcesQuery(Guid ClientId);

public sealed class GetKeyMappingResourcesValidator : AbstractValidator<GetKeyMappingResourcesQuery>
{
    public GetKeyMappingResourcesValidator()
    {
        RuleFor(query => query.ClientId).NotEmpty();
    }
}

public sealed class GetKeyMappingResourcesHandler(ConfigurationKeysApi api)
{
    public async Task<NumoResult<IReadOnlyList<KeyMappingOption>>> HandleAsync(
        GetKeyMappingResourcesQuery query,
        CancellationToken cancellationToken)
    {
        var resources = await api.GetAsync<List<ConfigurationResource>>(
            $"api/clients/{query.ClientId}/resources",
            $"the resources of client {query.ClientId}",
            cancellationToken);

        return resources.IsFailed
            ? NumoResult.Fail<IReadOnlyList<KeyMappingOption>>(resources.Errors)
            : NumoResult.Ok(KeyMappingOptions.From(resources.Value.Select(resource => (resource.Id, resource.Name))));
    }
}
