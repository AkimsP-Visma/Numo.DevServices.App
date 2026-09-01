namespace Numo.DevServices.Api.Features.SampleItems;

[ApiController]
[Route("api/sample-items")]
public sealed class SampleItemsController(INumoMediator mediator) : ControllerBase
{
    [HttpGet]
    public Task<NumoResult<PaginatedList<GetSampleItemsResult>>> GetAll(
        [FromQuery] GetSampleItemsQuery query,
        CancellationToken cancellationToken)
        => mediator.SendAsync<PaginatedList<GetSampleItemsResult>>(query, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<NumoResult<GetSampleItemByIdResult>> Get(Guid id, CancellationToken cancellationToken)
        => mediator.SendAsync<GetSampleItemByIdResult>(new GetSampleItemByIdQuery(id), cancellationToken);

    [HttpPost]
    public Task<NumoResult<CreateSampleItemResult>> Create(
        CreateSampleItemCommand command,
        CancellationToken cancellationToken)
        => mediator.SendAsync<CreateSampleItemResult>(command, cancellationToken);
}
