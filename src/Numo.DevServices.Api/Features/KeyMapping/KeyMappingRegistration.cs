using Numo.Common.Lib.ServiceDiscovery;

namespace Numo.DevServices.Api.Features.KeyMapping;

internal static class KeyMappingRegistration
{
    public const string ConfigurationApiHttpClientName = "NumoDataIntegrationConfigurationKeyMapping";

    private const string ConfigurationApiServiceDiscoveryKey = "Numo.DataIntegration.Configuration.Api";

    private static readonly TimeSpan ConfigurationApiRequestTimeout = TimeSpan.FromSeconds(60);

    public static IServiceCollection AddKeyMappingFeature(this IServiceCollection services)
    {
        // Numo.DataIntegration.Configuration.Lib does have both conversions, but it takes client and
        // resource names and re-resolves them by name, while this slice already holds their ids.
        services
            .AddHttpClient(ConfigurationApiHttpClientName)
            .ConfigureHttpClient((serviceProvider, httpClient) =>
            {
                // Resolved per client creation, so an environment switch takes effect on the next request.
                httpClient.BaseAddress = serviceProvider
                    .GetRequiredService<IServiceDiscoveryService>()
                    .GetServiceLocation(ConfigurationApiServiceDiscoveryKey);
                httpClient.Timeout = ConfigurationApiRequestTimeout;
            });

        // These implement no interface, so the convention scan does not register them.
        services.AddScoped<ConfigurationKeysApi>();
        services.AddScoped<ResourceConnectorLookup>();

        return services;
    }
}
