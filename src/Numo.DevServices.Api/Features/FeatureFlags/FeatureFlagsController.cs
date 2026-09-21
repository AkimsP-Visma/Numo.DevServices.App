namespace Numo.DevServices.Api.Features.FeatureFlags;

[ApiController]
[Route("api/feature-flags")]
public sealed class FeatureFlagsController(INumoMediator mediator) : ControllerBase
{
    [HttpGet]
    public Task<NumoResult<GetFeatureFlagsResult>> GetAll(CancellationToken cancellationToken)
        => mediator.SendAsync<GetFeatureFlagsResult>(new GetFeatureFlagsQuery(), cancellationToken);
}
