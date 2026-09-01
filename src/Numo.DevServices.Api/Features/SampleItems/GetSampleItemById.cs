using Numo.DevServices.Api.Persistence;

namespace Numo.DevServices.Api.Features.SampleItems;

public sealed record GetSampleItemByIdQuery(Guid Id);

public sealed record GetSampleItemByIdResult(Guid Id, string Name, string? Description)
{
    public static GetSampleItemByIdResult From(SampleItem item)
        => new(item.Id, item.Name, item.Description);
}

public sealed class GetSampleItemByIdValidator : AbstractValidator<GetSampleItemByIdQuery>
{
    public GetSampleItemByIdValidator()
    {
        RuleFor(query => query.Id).NotEmpty();
    }
}

public sealed class GetSampleItemByIdHandler(DevServicesDbContext dbContext)
{
    public async Task<NumoResult<GetSampleItemByIdResult>> HandleAsync(
        GetSampleItemByIdQuery query,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.SampleItems
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == query.Id, cancellationToken);

        return item is null
            ? NumoResult.Fail<GetSampleItemByIdResult>(SampleItemErrors.NotFound(query.Id))
            : NumoResult.Ok(GetSampleItemByIdResult.From(item));
    }
}
