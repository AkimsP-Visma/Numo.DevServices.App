using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Numo.DevServices.Api.Features.ServiceData.Resources;

/// <summary>
/// The one HTTP surface every DataIntegration resource reads over. No client library fits this
/// section's browsing needs: the real <c>IConfigurationClient</c> (dumped by reflection and saved
/// under .superpowers/handoff/dataintegration-client-signatures.md) has no list method for clients,
/// pipelines, connectors, connections, client-resources, pipeline-resources, pipeline-executions or
/// execution-steps, and no by-id method for the nested certificates or credentials routes - it is
/// built for point lookups by name, not for browsing. So every resource in this section hand-rolls
/// its request the way <see cref="DepartmentRolesResource"/> does for the Employee service, except
/// there is no envelope to parse: a live probe against test.numo.lv verified the Configuration API
/// answers with plain JSON - a bare array or a bare object, no <c>{value, isSuccessful, errors}</c>
/// wrapper - and needs no tenant header at all.
///
/// Public, not internal, for the same reason as <see cref="IServiceDataResource"/>: a public
/// resource class cannot take an internal constructor parameter (CS0051).
/// </summary>
public sealed class DataIntegrationConfigurationApi(IHttpClientFactory httpClientFactory)
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>A list or a flat-object route that always answers with a body on success - every
    /// route in this section except the by-id ones, which use <see cref="GetOrNullAsync{T}"/>.</summary>
    public Task<T> GetAsync<T>(string relativeUrl, string callDescription, CancellationToken cancellationToken)
        => DownstreamCall.InvokeAsync(
            async () =>
            {
                var httpClient = CreateClient();
                using var response = await httpClient.GetAsync(relativeUrl, cancellationToken);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<T>(ResponseJsonOptions, cancellationToken)
                    ?? throw new InvalidOperationException($"The response for {callDescription} carried no body.");
            },
            callDescription);

    /// <summary>A by-id route, where a 404 is the record not existing rather than a call failure -
    /// there is no envelope metadata to recognise absence from, only the status code itself.</summary>
    public Task<T?> GetOrNullAsync<T>(string relativeUrl, string callDescription, CancellationToken cancellationToken)
        where T : class
        => DownstreamCall.InvokeAsync(
            async () =>
            {
                var httpClient = CreateClient();
                using var response = await httpClient.GetAsync(relativeUrl, cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<T>(ResponseJsonOptions, cancellationToken);
            },
            callDescription);

    /// <summary>
    /// For the one route in this section whose declared response shape is ambiguous:
    /// <c>/api/pipeline-executions/{executionId}</c> is typed in the saved OpenAPI spec as
    /// returning an array of <typeparamref name="T"/>, which does not fit a by-id route and was
    /// never live-verified. Tries the array shape first and falls back to a bare object, so a
    /// wrong guess here does not become a downstream failure for every caller.
    /// </summary>
    public Task<T?> GetOrNullEitherShapeAsync<T>(
        string relativeUrl,
        string callDescription,
        CancellationToken cancellationToken)
        where T : class
        => DownstreamCall.InvokeAsync(
            async () =>
            {
                var httpClient = CreateClient();
                using var response = await httpClient.GetAsync(relativeUrl, cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);

                try
                {
                    var items = JsonSerializer.Deserialize<List<T>>(body, ResponseJsonOptions);
                    return items?.FirstOrDefault();
                }
                catch (JsonException)
                {
                    return JsonSerializer.Deserialize<T>(body, ResponseJsonOptions);
                }
            },
            callDescription);

    private HttpClient CreateClient()
        => httpClientFactory.CreateClient(ServiceDataRegistration.DataIntegrationConfigurationHttpClientName);
}
