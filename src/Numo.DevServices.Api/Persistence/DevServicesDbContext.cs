using Numo.DevServices.Api.Features.SampleItems;

namespace Numo.DevServices.Api.Persistence;

public sealed class DevServicesDbContext(DbContextOptions options) : DbContext(options), IUnitOfWork
{
    public const string SchemaName = "DevServices";

    public DbSet<SampleItem> SampleItems => Set<SampleItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DevServicesDbContext).Assembly);
    }
}
