namespace Numo.DevServices.Api.Features.ServiceHealth;

[ApiController]
[Route("api/service-health")]
public sealed class ServiceHealthController(INumoMediator mediator) : ControllerBase
{
    [HttpGet]
    public Task<NumoResult<GetServiceHealthResult>> Get(CancellationToken cancellationToken)
        => mediator.SendAsync<GetServiceHealthResult>(new GetServiceHealthQuery(), cancellationToken);
}
