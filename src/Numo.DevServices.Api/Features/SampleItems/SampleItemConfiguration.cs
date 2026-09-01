using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Numo.Core.Infrastructure.Persistence.Configuration;

namespace Numo.DevServices.Api.Features.SampleItems;

// Field constraints come from SampleItemSpecification, applied by the framework base configuration.
public sealed class SampleItemConfiguration : BaseEntityConfiguration<SampleItem>
{
    public override void Configure(EntityTypeBuilder<SampleItem> builder)
    {
        base.Configure(builder);

        builder.ToTable("SampleItems");
        builder.HasIndex(item => item.Name).IsUnique();
    }
}
