using System.Diagnostics.CodeAnalysis;
using Numo.Core.Infrastructure.Features.Sql;

namespace Numo.DevServices.Api.Persistence;

/// <summary>
/// Used by "dotnet ef" only; the running host builds its context through AddNumoSql.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class DevServicesDbContextDesignFactory : NumoDesignTimeDbContextFactory<DevServicesDbContext>
{
    protected override string SchemaName => DevServicesDbContext.SchemaName;
}
