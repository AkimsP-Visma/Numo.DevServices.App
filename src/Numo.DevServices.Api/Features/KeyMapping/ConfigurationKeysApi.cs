using System.Text.Json;

namespace Numo.DevServices.Api.Features.KeyMapping;

/// <summary>
/// The slice's only route to the Configuration API. It answers with plain JSON (no envelope) and
/// needs no tenant header. A failure becomes <see cref="KeyMappingErrors.DownstreamCallFailed"/>
/// here, so handlers never see an HTTP exception.
/// Public because a public handler cannot take an internal parameter type (CS0051).
/// </summary>
public sealed class ConfigurationKeysApi(IHttpClientFactory httpClientFactory, ILogger<ConfigurationKeysApi> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<NumoResult<T>> GetAsync<T>(string relativeUrl, string callDescription, CancellationToken cancellationToken)
        => SendAsync<T>(
            httpClient => httpClient.GetAsync(relativeUrl, cancellationToken),
            callDescription,
            cancellationToken);

    public Task<NumoResult<T>> PostAsync<TBody, T>(
        string relativeUrl,
        TBody body,
        string callDescription,
        CancellationToken cancellationToken)
        => SendAsync<T>(
            httpClient => httpClient.PostAsJsonAsync(relativeUrl, body, JsonOptions, cancellationToken),
            callDescription,
            cancellationToken);

    private async Task<NumoResult<T>> SendAsync<T>(
        Func<HttpClient, Task<HttpResponseMessage>> send,
        string callDescription,
        CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient(KeyMappingRegistration.ConfigurationApiHttpClientName);

        try
        {
            using var response = await send(httpClient);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Configuration API call for {CallDescription} returned {StatusCode}.",
                    callDescription,
                    (int)response.StatusCode);

                return NumoResult.Fail<T>(KeyMappingErrors.DownstreamCallFailed(callDescription, (int)response.StatusCode));
            }

            var document = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);

            return document is null
                ? NumoResult.Fail<T>(KeyMappingErrors.DownstreamCallFailed(callDescription, null))
                : NumoResult.Ok(document);
        }
        // A cancellation by the caller propagates; an HttpClient timeout arrives as the same type,
        // but with a TimeoutException inside, and is a downstream failure.
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException
            || exception is TaskCanceledException { InnerException: TimeoutException })
        {
            logger.LogWarning(exception, "Configuration API call for {CallDescription} failed.", callDescription);

            return NumoResult.Fail<T>(KeyMappingErrors.DownstreamCallFailed(callDescription, null));
        }
    }
}
