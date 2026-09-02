using System.Text.Json;
using System.Text.Json.Nodes;
using Numo.Common.Lib.ServiceDiscovery;

namespace Numo.DevServices.Api.Features.Services;

public sealed record GetServiceOpenApiQuery(string ServiceName);

public sealed class GetServiceOpenApiValidator : AbstractValidator<GetServiceOpenApiQuery>
{
    public GetServiceOpenApiValidator()
    {
        RuleFor(query => query.ServiceName).NotEmpty();
    }
}

/// <summary>
/// Fetches a service's OpenAPI document server-side so the browser never calls the service itself.
/// The service can only be named by a key present in configuration, so this is not a general-purpose proxy.
/// </summary>
public sealed class GetServiceOpenApiHandler(
    IServiceDiscoveryService serviceDiscovery,
    IHttpClientFactory httpClientFactory,
    ILogger<GetServiceOpenApiHandler> logger)
{
    // Every Numo service exposes its generated document here; a service that does not will simply fail to load.
    private const string OpenApiDocumentPath = "swagger/v1/swagger.json";

    private const string ConfiguredServerDescription = "Configured location, called straight from the browser";

    public async Task<NumoResult<JsonNode>> HandleAsync(
        GetServiceOpenApiQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetServiceLocation(query.ServiceName, out var serviceLocation))
        {
            return NumoResult.Fail<JsonNode>(ServicesErrors.UnknownService(query.ServiceName));
        }

        var openApiUrl = new Uri(serviceLocation, OpenApiDocumentPath);

        var httpClient = httpClientFactory.CreateClient(ServicesRegistration.OpenApiHttpClientName);

        try
        {
            using var response = await httpClient.GetAsync(openApiUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "OpenAPI request for {ServiceName} to {OpenApiUrl} returned {StatusCode}.",
                    query.ServiceName, openApiUrl, (int)response.StatusCode);

                return NumoResult.Fail<JsonNode>(
                    ServicesErrors.OpenApiUnavailable(query.ServiceName, $"{openApiUrl} returned {(int)response.StatusCode}."));
            }

            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            var document = await JsonNode.ParseAsync(content, cancellationToken: cancellationToken);

            if (document is null)
            {
                return NumoResult.Fail<JsonNode>(
                    ServicesErrors.OpenApiUnavailable(query.ServiceName, $"{openApiUrl} returned an empty document."));
            }

            PrependConfiguredServer(document, serviceLocation);
            return NumoResult.Ok(document);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogWarning(
                exception,
                "OpenAPI request for {ServiceName} to {OpenApiUrl} failed.",
                query.ServiceName, openApiUrl);

            return NumoResult.Fail<JsonNode>(
                ServicesErrors.OpenApiUnavailable(query.ServiceName, exception.Message));
        }
    }

    private bool TryGetServiceLocation(string serviceName, out Uri serviceLocation)
    {
        try
        {
            var location = serviceDiscovery.GetServiceLocation(serviceName);

            // Configured locations are inconsistent about the trailing slash, which Uri needs to keep the last segment.
            serviceLocation = new Uri($"{location.AbsoluteUri.TrimEnd('/')}/");
            return true;
        }
        catch (ServiceDoesNotExistException)
        {
            serviceLocation = null!;
            return false;
        }
    }

    /// <summary>
    /// Numo services declare relative servers, which Swagger UI resolves against this app's own origin,
    /// so "Try it out" would call the frontend instead of the service. Offering the configured location
    /// as the first server points those requests at the service itself.
    /// </summary>
    private static void PrependConfiguredServer(JsonNode document, Uri serviceLocation)
    {
        if (document["servers"] is not JsonArray servers)
        {
            return;
        }

        servers.Insert(0, new JsonObject
        {
            ["url"] = serviceLocation.AbsoluteUri.TrimEnd('/'),
            ["description"] = ConfiguredServerDescription,
        });
    }
}
