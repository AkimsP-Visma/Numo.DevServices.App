using Microsoft.Extensions.DependencyInjection.Extensions;
using Numo.Common.Lib.ServiceDiscovery;

namespace Numo.DevServices.Api.Features.Environments;

internal static class EnvironmentsRegistration
{
    public static IServiceCollection AddEnvironmentsFeature(this IServiceCollection services)
    {
        services
            .AddOptions<EnvironmentsOptions>()
            .BindConfiguration(EnvironmentsOptions.ConfigurationSectionName);

        // CurrentEnvironmentStore implements no interface, so - like ServiceData's PersonNameLookup
        // and DataIntegrationConfigurationApi - the convention scan will not find it; it needs this
        // explicit registration.
        services.AddSingleton<CurrentEnvironmentStore>();

        // Replace, not AddSingleton: Numo.Common.Lib registers ConfigurationServiceDiscoveryService
        // with TryAddSingleton, and every existing caller of IServiceDiscoveryService already depends
        // on the interface, not the concrete type, so swapping the implementation here is enough to
        // make all of them environment-aware without touching any of them. Must run after
        // AddNumoCommonServices, which is what DevServicesModule already does.
        services.Replace(
            ServiceDescriptor.Singleton<IServiceDiscoveryService, EnvironmentAwareServiceDiscovery>());

        return services;
    }
}
