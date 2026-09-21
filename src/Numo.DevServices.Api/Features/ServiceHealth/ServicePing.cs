using System.Diagnostics;

namespace Numo.DevServices.Api.Features.ServiceHealth;

/// <summary>
/// Calls the ping endpoint every Numo service exposes at the same path under its configured location.
/// Up means HTTP 200 with a "pong" body; anything else, including a transport failure, is down.
/// </summary>
public sealed class ServicePing(IHttpClientFactory httpClientFactory, ILogger<ServicePing> logger)
{
    private const string PingPath = "api/platform/microservice/ping";

    private const string ExpectedPingBody = "pong";

    private const int OkStatusCode = 200;

    // A down service often answers with a whole error page, which has no place in a dashboard cell.
    private const int MaxReportedBodyLength = 120;

    public async Task<ServiceHealthStatus> PingAsync(string name, string location, CancellationToken cancellationToken)
    {
        if (!TryBuildPingUrl(location, out var pingUrl))
        {
            return Down(name, location, statusCode: null, $"'{location}' is not a usable absolute URL.", elapsedMs: 0);
        }

        var httpClient = httpClientFactory.CreateClient(ServiceHealthRegistration.PingHttpClientName);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await httpClient.GetAsync(pingUrl, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            stopwatch.Stop();

            var statusCode = (int)response.StatusCode;

            if (statusCode != OkStatusCode)
            {
                return Down(name, location, statusCode, $"Ping answered {statusCode}.", stopwatch.ElapsedMilliseconds);
            }

            if (!IsPong(body))
            {
                return Down(
                    name,
                    location,
                    statusCode,
                    $"Ping answered 200 with '{Shorten(body)}' instead of '{ExpectedPingBody}'.",
                    stopwatch.ElapsedMilliseconds);
            }

            return new ServiceHealthStatus(name, location, IsUp: true, statusCode, DownReason: null, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller gave up, which says nothing about the service.
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            stopwatch.Stop();
            return Down(name, location, statusCode: null, exception.Message, stopwatch.ElapsedMilliseconds);
        }
    }

    private static bool TryBuildPingUrl(string location, out Uri pingUrl)
    {
        // Configured locations are inconsistent about the trailing slash, which Uri needs to keep the last segment.
        return Uri.TryCreate($"{location.TrimEnd('/')}/{PingPath}", UriKind.Absolute, out pingUrl!);
    }

    private static bool IsPong(string body)
        => string.Equals(body.Trim(), ExpectedPingBody, StringComparison.OrdinalIgnoreCase);

    private static string Shorten(string body)
    {
        var normalized = body.Trim().ReplaceLineEndings(" ");

        return normalized.Length <= MaxReportedBodyLength
            ? normalized
            : $"{normalized[..MaxReportedBodyLength]}...";
    }

    private ServiceHealthStatus Down(string name, string location, int? statusCode, string reason, long elapsedMs)
    {
        logger.LogWarning("Service {ServiceName} at {ServiceLocation} is down: {DownReason}", name, location, reason);

        return new ServiceHealthStatus(name, location, IsUp: false, statusCode, reason, elapsedMs);
    }
}
