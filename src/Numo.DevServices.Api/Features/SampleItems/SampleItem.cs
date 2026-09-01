namespace Numo.DevServices.Api.Features.SampleItems;

/// <summary>
/// Placeholder entity proving the pipeline end-to-end. Delete together with the rest of the
/// SampleItems slice when the first real feature lands.
/// </summary>
public sealed class SampleItem : BaseEntity
{
    public static SampleItem Create(string name, string? description)
        => new(Guid.NewGuid(), name, description);

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    private SampleItem(Guid id, string name, string? description) : base(id)
    {
        Name = name;
        Description = description;
    }

    // Materialisation constructor for EF Core.
    private SampleItem()
    {
    }
}
