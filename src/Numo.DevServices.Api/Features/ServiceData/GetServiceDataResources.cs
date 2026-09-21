namespace Numo.DevServices.Api.Features.ServiceData;

public sealed record GetServiceDataResourcesQuery;

public sealed record GetServiceDataResourcesResult(IReadOnlyList<ResourceDescriptor> Resources);

// The mediator fails the request outright when a request type has no validator, so a rule-less one is required.
public sealed class GetServiceDataResourcesValidator : AbstractValidator<GetServiceDataResourcesQuery>;

/// <summary>
/// The catalogue the frontend builds its resource picker, its grid columns and its filter bar from.
/// Descriptors are compile-time metadata, so no service is called and no tenant is needed.
/// </summary>
public sealed class GetServiceDataResourcesHandler(ServiceDataCatalogue catalogue)
{
    public Task<NumoResult<GetServiceDataResourcesResult>> HandleAsync(
        GetServiceDataResourcesQuery query,
        CancellationToken cancellationToken)
        => Task.FromResult(NumoResult.Ok(new GetServiceDataResourcesResult(catalogue.Descriptors)));
}
