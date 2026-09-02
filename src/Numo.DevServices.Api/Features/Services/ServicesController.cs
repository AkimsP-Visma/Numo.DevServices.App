using System.Text.Json.Nodes;

namespace Numo.DevServices.Api.Features.Services;

[ApiController]
[Route("api/services")]
public sealed class ServicesController(INumoMediator mediator) : ControllerBase
{
    [HttpGet]
    public Task<NumoResult<IReadOnlyList<GetServicesResult>>> GetAll(CancellationToken cancellationToken)
        => mediator.SendAsync<IReadOnlyList<GetServicesResult>>(new GetServicesQuery(), cancellationToken);

    [HttpGet("{serviceName}/openapi")]
    public Task<NumoResult<JsonNode>> GetOpenApi(string serviceName, CancellationToken cancellationToken)
        => mediator.SendAsync<JsonNode>(new GetServiceOpenApiQuery(serviceName), cancellationToken);
}
