namespace Numo.DevServices.Api.Features.Environments;

[ApiController]
[Route("api/environments")]
public sealed class EnvironmentsController(INumoMediator mediator) : ControllerBase
{
    [HttpGet]
    public Task<NumoResult<GetCurrentEnvironmentResult>> GetCurrent(CancellationToken cancellationToken)
        => mediator.SendAsync<GetCurrentEnvironmentResult>(new GetCurrentEnvironmentQuery(), cancellationToken);

    [HttpPut("current")]
    public Task<NumoResult<SetEnvironmentResult>> SetCurrent(
        [FromBody] SetEnvironmentCommand command,
        CancellationToken cancellationToken)
        => mediator.SendAsync<SetEnvironmentResult>(command, cancellationToken);
}
