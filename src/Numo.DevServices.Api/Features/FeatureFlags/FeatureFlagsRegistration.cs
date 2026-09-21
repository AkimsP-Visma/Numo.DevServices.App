using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace Numo.DevServices.Api.Features.FeatureFlags;

internal static class FeatureFlagsRegistration
{
    public const string LaunchDarklyApiHttpClientName = "LaunchDarklyApi";

    private static readonly Uri LaunchDarklyApiBaseAddress = new("https://app.launchdarkly.com/");

    private static readonly TimeSpan LaunchDarklyApiRequestTimeout = TimeSpan.FromSeconds(30);

    public static IServiceCollection AddFeatureFlagsFeature(this IServiceCollection services)
    {
        services
            .AddOptions<LaunchDarklyApiOptions>()
            .BindConfiguration(LaunchDarklyApiOptions.ConfigurationSectionName);

        services
            .AddHttpClient(LaunchDarklyApiHttpClientName)
            .ConfigureHttpClient((serviceProvider, httpClient) =>
            {
                httpClient.BaseAddress = LaunchDarklyApiBaseAddress;
                httpClient.Timeout = LaunchDarklyApiRequestTimeout;

                var apiToken = serviceProvider
                    .GetRequiredService<IOptionsMonitor<LaunchDarklyApiOptions>>()
                    .CurrentValue
                    .ApiToken;

                if (!string.IsNullOrWhiteSpace(apiToken))
                {
                    // Assigned rather than added, so that a second registration of this action
                    // overwrites the header instead of sending the token twice over, which the API
                    // rejects. The token is the whole header value, so it takes the scheme position.
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(apiToken);
                }
            });

        return services;
    }
}
