using Numo.Common.Lib.ServiceDiscovery;

namespace Numo.DevServices.Api.Features.ServiceHealth;

public sealed record GetServiceHealthQuery;

/// <summary>The outcome of one ping. <paramref name="DownReason"/> is filled only when down.</summary>
public sealed record ServiceHealthStatus(
    string Name,
    string Location,
    bool IsUp,
    int? StatusCode,
    string? DownReason,
    long ResponseTimeMs);

public sealed record GetServiceHealthResult(
    DateTimeOffset CheckedAt,
    IReadOnlyList<ServiceHealthStatus> Services);

// The mediator fails the request outright when a request type has no validator, so a rule-less one is required.
public sealed class GetServiceHealthValidator : AbstractValidator<GetServiceHealthQuery>;

/// <summary>
/// Pings every configured service. A service being down is data, not a failure, so the request
/// succeeds as long as the configuration could be read.
/// </summary>
public sealed class GetServiceHealthHandler(
    IServiceDiscoveryService serviceDiscovery,
    ServicePing servicePing,
    ILogger<GetServiceHealthHandler> logger)
{
    public async Task<NumoResult<GetServiceHealthResult>> HandleAsync(
        GetServiceHealthQuery query,
        CancellationToken cancellationToken)
    {
        var pings = GetConfiguredServices()
            .Select(service => servicePing.PingAsync(service.Name, service.Location, cancellationToken))
            .ToList();

        var statuses = await Task.WhenAll(pings);

        IReadOnlyList<ServiceHealthStatus> ordered = statuses.OrderBy(status => status.Name).ToList();
        return NumoResult.Ok(new GetServiceHealthResult(DateTimeOffset.UtcNow, ordered));
    }

    private IEnumerable<(string Name, string Location)> GetConfiguredServices()
    {
        foreach (var service in serviceDiscovery.GetAllServices())
        {
            // Discovery yields an entry for every key under "Services", even one whose Location is missing.
            if (string.IsNullOrWhiteSpace(service.Location))
            {
                logger.LogWarning("Service {ServiceName} has no Location configured and is not pinged.", service.Name);
                continue;
            }

            yield return (service.Name, service.Location);
        }
    }
}
