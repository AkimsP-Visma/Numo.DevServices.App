using Numo.Core.Application.Validation.Validators;
using Numo.Core.Extensions;
using Numo.DevServices.Api.Persistence;

namespace Numo.DevServices.Api.Features.SampleItems;

public sealed record GetSampleItemsQuery(PagedDataRequest PagedRequest);

public sealed record GetSampleItemsResult(Guid Id, string Name, string? Description)
{
    public static GetSampleItemsResult From(SampleItem item)
        => new(item.Id, item.Name, item.Description);
}

public sealed class GetSampleItemsValidator : AbstractValidator<GetSampleItemsQuery>
{
    public GetSampleItemsValidator()
    {
        RuleFor(query => query.PagedRequest).SetValidator(new PagedDataRequestValidator());
    }
}

public sealed class GetSampleItemsHandler(DevServicesDbContext dbContext)
{
    public async Task<NumoResult<PaginatedList<GetSampleItemsResult>>> HandleAsync(
        GetSampleItemsQuery query,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.SampleItems
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var page = Sort(items, query.PagedRequest).ApplyPagedRequest(query.PagedRequest);

        return NumoResult.Ok(page.Map(GetSampleItemsResult.From));
    }

    private static IEnumerable<SampleItem> Sort(IEnumerable<SampleItem> items, PagedDataRequest pagedRequest)
        => pagedRequest.Sorting.Direction == OrderDirection.Asc
            ? items.OrderBy(item => item.Name)
            : items.OrderByDescending(item => item.Name);
}
