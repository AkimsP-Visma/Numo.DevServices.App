using Numo.Common.Lib.ServiceDiscovery;

namespace Numo.DevServices.Api.Features.Services;

public sealed record GetServicesQuery;

public sealed record GetServicesResult(string Name, string Location);

// The mediator fails the request outright when a request type has no validator, so a rule-less one is required.
public sealed class GetServicesValidator : AbstractValidator<GetServicesQuery>;

public sealed class GetServicesHandler(
    IServiceDiscoveryService serviceDiscovery,
    ILogger<GetServicesHandler> logger)
{
    public Task<NumoResult<IReadOnlyList<GetServicesResult>>> HandleAsync(
        GetServicesQuery query,
        CancellationToken cancellationToken)
    {
        var services = new List<GetServicesResult>();

        foreach (var service in serviceDiscovery.GetAllServices())
        {
            // Discovery yields an entry for every key under "Services", even one whose Location is missing.
            if (string.IsNullOrWhiteSpace(service.Location))
            {
                logger.LogWarning("Service {ServiceName} has no Location configured and is not offered.", service.Name);
                continue;
            }

            services.Add(new GetServicesResult(service.Name, service.Location));
        }

        IReadOnlyList<GetServicesResult> ordered = services.OrderBy(service => service.Name).ToList();
        return Task.FromResult(NumoResult.Ok(ordered));
    }
}
