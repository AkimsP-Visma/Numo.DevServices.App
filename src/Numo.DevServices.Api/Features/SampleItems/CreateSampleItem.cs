using Numo.Core.Application.Validation;
using Numo.DevServices.Api.Persistence;

namespace Numo.DevServices.Api.Features.SampleItems;

public sealed record CreateSampleItemCommand(string Name, string? Description);

public sealed record CreateSampleItemResult(Guid Id);

public sealed class CreateSampleItemValidator : AbstractValidator<CreateSampleItemCommand>
{
    public CreateSampleItemValidator()
    {
        var specification = SampleItemSpecification.Instance;

        RuleFor(command => command.Name).From(specification, item => item.Name);
        RuleFor(command => command.Description).From(specification, item => item.Description);
    }
}

public sealed class CreateSampleItemHandler(DevServicesDbContext dbContext)
{
    public async Task<NumoResult<CreateSampleItemResult>> HandleAsync(
        CreateSampleItemCommand command,
        CancellationToken cancellationToken)
    {
        var isNameTaken = await dbContext.SampleItems
            .AnyAsync(item => item.Name == command.Name, cancellationToken);

        if (isNameTaken)
        {
            return NumoResult.Fail<CreateSampleItemResult>(SampleItemErrors.NameAlreadyUsed(command.Name));
        }

        var item = SampleItem.Create(command.Name, command.Description);

        dbContext.SampleItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return NumoResult.Ok(new CreateSampleItemResult(item.Id));
    }
}
