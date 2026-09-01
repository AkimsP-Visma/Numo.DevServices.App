using System.Diagnostics.CodeAnalysis;

namespace Numo.DevServices.Api;

/// <summary>
/// Marks this assembly for numo-core: AddNumoMediator scans it for handlers and validators,
/// and slice-local services are registered here.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class DevServicesModule : IBusinessModule
{
    public void ConfigureServices(IServiceCollection services)
    {
    }
}
