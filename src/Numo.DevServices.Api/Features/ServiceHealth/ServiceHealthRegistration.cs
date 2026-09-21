namespace Numo.DevServices.Api.Features.ServiceHealth;

internal static class ServiceHealthRegistration
{
    public const string PingHttpClientName = "ServicePing";

    // Short on purpose: a ping that takes this long is a dashboard-worthy outage anyway, and every
    // configured service is pinged on the same request.
    private static readonly TimeSpan PingRequestTimeout = TimeSpan.FromSeconds(10);

    public static IServiceCollection AddServiceHealthFeature(this IServiceCollection services)
    {
        services
            .AddHttpClient(PingHttpClientName)
            .ConfigureHttpClient(httpClient => httpClient.Timeout = PingRequestTimeout);

        services.AddTransient<ServicePing>();

        return services;
    }
}
