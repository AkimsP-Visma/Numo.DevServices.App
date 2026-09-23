using System.Diagnostics.CodeAnalysis;
using Numo.Common.Lib.Extensions;
using Numo.DevServices.Api.Features.Environments;
using Numo.DevServices.Api.Features.FeatureFlags;
using Numo.DevServices.Api.Features.ServiceHealth;
using Numo.DevServices.Api.Features.ServiceData;
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
        // Brings in IServiceDiscoveryService; AddEnvironmentsFeature below replaces its
        // implementation, so it must run after this call.
        services.AddNumoCommonServices();

        services.AddEnvironmentsFeature();
        services.AddServicesFeature();
        services.AddServiceHealthFeature();
        services.AddFeatureFlagsFeature();
        services.AddServiceDataFeature();
    }
}
