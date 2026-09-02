namespace Numo.DevServices.Api.Features.Services;

internal static class ServicesRegistration
{
    public const string OpenApiHttpClientName = "ServiceOpenApi";

    private static readonly TimeSpan OpenApiRequestTimeout = TimeSpan.FromSeconds(30);

    public static IServiceCollection AddServicesFeature(this IServiceCollection services)
    {
        services
            .AddHttpClient(OpenApiHttpClientName)
            .ConfigureHttpClient(httpClient => httpClient.Timeout = OpenApiRequestTimeout);

        return services;
    }
}
