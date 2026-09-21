using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace Numo.DevServices.Api.Features.FeatureFlags;

public sealed record GetFeatureFlagsQuery;

// The mediator fails the request outright when a request type has no validator, so a rule-less one is required.
public sealed class GetFeatureFlagsValidator : AbstractValidator<GetFeatureFlagsQuery>;

/// <summary>
/// What one flag serves in one environment by its default rule. A null <see cref="Value"/> means
/// no variation is marked, so the value is whatever default the calling SDK passes in.
/// </summary>
public sealed record FeatureFlagEnvironmentState(bool IsOn, JsonNode? Value, bool IsRollout);

public sealed record FeatureFlagRow(
    string Key,
    string Name,
    string? Description,
    IReadOnlyList<string> Tags,
    IReadOnlyDictionary<string, FeatureFlagEnvironmentState> Environments);

/// <summary>
/// <see cref="EnvironmentKeys"/> is the column order for the page, so the frontend needs no
/// knowledge of which environments the project has.
/// </summary>
public sealed record GetFeatureFlagsResult(
    IReadOnlyList<string> EnvironmentKeys,
    IReadOnlyList<FeatureFlagRow> Flags);

/// <summary>
/// Lists a project's flags through the LaunchDarkly REST API. The server SDK cannot answer this:
/// it evaluates a named flag against a context, and tags and names are never sent to SDKs at all.
/// </summary>
public sealed class GetFeatureFlagsHandler(
    IOptionsMonitor<LaunchDarklyApiOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<GetFeatureFlagsHandler> logger)
{
    // The flags endpoint returns 20 per page unless told otherwise, so paging is unavoidable.
    private const int FlagPageSize = 100;

    // Bounds the loop in case a full page ever stops advancing the offset.
    private const int MaxFlagPageCount = 50;

    // A project holds few environments, so one generous page covers them.
    private const int EnvironmentPageSize = 100;

    public async Task<NumoResult<GetFeatureFlagsResult>> HandleAsync(
        GetFeatureFlagsQuery query,
        CancellationToken cancellationToken)
    {
        var apiOptions = options.CurrentValue;

        if (string.IsNullOrWhiteSpace(apiOptions.ApiToken) || string.IsNullOrWhiteSpace(apiOptions.ProjectKey))
        {
            return NumoResult.Fail<GetFeatureFlagsResult>(FeatureFlagsErrors.NotConfigured());
        }

        var httpClient = httpClientFactory.CreateClient(FeatureFlagsRegistration.LaunchDarklyApiHttpClientName);
        var projectKey = apiOptions.ProjectKey;

        var environmentKeys = await GetEnvironmentKeysAsync(httpClient, projectKey, cancellationToken);

        if (environmentKeys.IsFailed)
        {
            return NumoResult.Fail<GetFeatureFlagsResult>(environmentKeys.Errors);
        }

        var flags = await GetAllFlagsAsync(httpClient, projectKey, environmentKeys.Value, cancellationToken);

        return flags.IsSuccessful
            ? NumoResult.Ok(LaunchDarklyFlagMapper.ToResult(environmentKeys.Value, flags.Value))
            : NumoResult.Fail<GetFeatureFlagsResult>(flags.Errors);
    }

    /// <summary>
    /// The flags endpoint leaves out per-environment state unless every environment is named in
    /// the request, so the project's environments have to be read first.
    /// </summary>
    private async Task<NumoResult<IReadOnlyList<string>>> GetEnvironmentKeysAsync(
        HttpClient httpClient,
        string projectKey,
        CancellationToken cancellationToken)
    {
        var requestUri =
            $"api/v2/projects/{Uri.EscapeDataString(projectKey)}/environments?limit={EnvironmentPageSize}";

        var response = await GetAsync<LaunchDarklyEnvironmentsResponse>(
            httpClient, requestUri, $"the environments of project {projectKey}", cancellationToken);

        if (response.IsFailed)
        {
            return NumoResult.Fail<IReadOnlyList<string>>(response.Errors);
        }

        // Kept in the API's own order, which runs from the earliest environment towards production.
        IReadOnlyList<string> environmentKeys = response.Value.Items
            .Select(environment => environment.Key)
            .Where(environmentKey => !string.IsNullOrWhiteSpace(environmentKey))
            .ToList();

        return NumoResult.Ok(environmentKeys);
    }

    private async Task<NumoResult<IReadOnlyList<LaunchDarklyFlag>>> GetAllFlagsAsync(
        HttpClient httpClient,
        string projectKey,
        IReadOnlyList<string> environmentKeys,
        CancellationToken cancellationToken)
    {
        var flags = new List<LaunchDarklyFlag>();

        for (var pageIndex = 0; pageIndex < MaxFlagPageCount; pageIndex++)
        {
            var requestUri = BuildFlagsRequestUri(projectKey, environmentKeys, pageIndex * FlagPageSize);

            var page = await GetAsync<LaunchDarklyFlagsResponse>(
                httpClient, requestUri, $"the flags of project {projectKey}", cancellationToken);

            if (page.IsFailed)
            {
                return NumoResult.Fail<IReadOnlyList<LaunchDarklyFlag>>(page.Errors);
            }

            flags.AddRange(page.Value.Items);

            if (page.Value.Items.Count < FlagPageSize)
            {
                return NumoResult.Ok<IReadOnlyList<LaunchDarklyFlag>>(flags);
            }
        }

        logger.LogWarning(
            "Stopped listing LaunchDarkly flags of project {ProjectKey} after {PageCount} pages.",
            projectKey, MaxFlagPageCount);

        return NumoResult.Ok<IReadOnlyList<LaunchDarklyFlag>>(flags);
    }

    private static string BuildFlagsRequestUri(
        string projectKey,
        IReadOnlyList<string> environmentKeys,
        int offset)
    {
        var requestUri = new StringBuilder(
            $"api/v2/flags/{Uri.EscapeDataString(projectKey)}?limit={FlagPageSize}&offset={offset}");

        // Naming the environments is what adds their state. The default summary representation is
        // then enough, while summary=0 would add every targeting rule this page does not show.
        foreach (var environmentKey in environmentKeys)
        {
            requestUri.Append($"&env={Uri.EscapeDataString(environmentKey)}");
        }

        return requestUri.ToString();
    }

    private async Task<NumoResult<T>> GetAsync<T>(
        HttpClient httpClient,
        string requestUri,
        string what,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(requestUri, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Reading {What} from LaunchDarkly returned {StatusCode}.",
                    what, (int)response.StatusCode);

                return NumoResult.Fail<T>(FeatureFlagsErrors.LaunchDarklyUnavailable(
                    $"reading {what} returned {(int)response.StatusCode}."));
            }

            var document = await response.Content.ReadFromJsonAsync<T>(cancellationToken);

            return document is null
                ? NumoResult.Fail<T>(FeatureFlagsErrors.LaunchDarklyUnavailable(
                    $"reading {what} returned an empty document."))
                : NumoResult.Ok(document);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Reading {What} from LaunchDarkly failed.", what);

            return NumoResult.Fail<T>(FeatureFlagsErrors.LaunchDarklyUnavailable(
                $"reading {what} failed: {exception.Message}"));
        }
    }
}
