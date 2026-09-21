using System.Diagnostics.CodeAnalysis;
using Numo.Common.Lib.Extensions;
using Numo.DevServices.Api.Features.FeatureFlags;
using Numo.DevServices.Api.Features.Services;

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
        // Brings in IServiceDiscoveryService, which reads the "Services" section; its implementation
        // is internal to the package, so this call is the only way to obtain it.
        services.AddNumoCommonServices();

        services.AddServicesFeature();
        services.AddFeatureFlagsFeature();
    }
}
