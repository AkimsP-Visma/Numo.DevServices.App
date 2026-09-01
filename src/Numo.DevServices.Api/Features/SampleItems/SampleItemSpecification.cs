using Numo.Core.Domain.Validation;

namespace Numo.DevServices.Api.Features.SampleItems;

/// <summary>
/// Single source of the field constraints: the EF configuration derives column limits from it
/// and validators derive their rules from it via <c>RuleFor(...).From(...)</c>.
/// </summary>
public sealed class SampleItemSpecification : NumoEntitySpecification<SampleItem>
{
    public static readonly SampleItemSpecification Instance = new();

    private const int NameMaxLength = 56;
    private const int DescriptionMaxLength = 2000;

    public SampleItemSpecification()
    {
        Field(item => item.Name).HasMaxLength(NameMaxLength);
        Field(item => item.Description).HasMaxLength(DescriptionMaxLength);
    }
}
