using Numo.Common.Lib.ServiceDiscovery;

namespace Numo.DevServices.Api.Features.Environments;

/// <summary>
/// Replaces Numo.Common.Lib's own ConfigurationServiceDiscoveryService (registered in
/// EnvironmentsRegistration via services.Replace, after AddNumoCommonServices), so every existing
/// caller of IServiceDiscoveryService - ServiceDataRegistration, GetServiceHealth,
/// GetServiceOpenApi, GetServices - becomes environment-aware without changing any of them: they
/// all already depend on the interface, never the concrete type.
///
/// Reads from CurrentEnvironmentStore.CurrentDefinition instead of a flat "Services" section, so an
/// environment switch is visible to the very next call - nothing here caches anything.
/// </summary>
public sealed class EnvironmentAwareServiceDiscovery(CurrentEnvironmentStore store) : IServiceDiscoveryService
{
    public IEnumerable<NumoService> GetAllServices()
        => store.CurrentDefinition.Services
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value.Location))
            .Select(entry => new NumoService(entry.Key, entry.Value.Location!, entry.Value.AppId));

    public Uri GetServiceLocation(string serviceId)
    {
        if (store.CurrentDefinition.Services.TryGetValue(serviceId, out var service)
            && !string.IsNullOrWhiteSpace(service.Location))
        {
            return new Uri(service.Location);
        }

        throw new ServiceDoesNotExistException(
            $"Could not find matching service key: {serviceId} in the '{store.Current}' environment.");
    }

    /// <summary>No environment defines an AppId - same as the section this replaces, per the
    /// existing project convention that GetServiceAppId always throws (see CLAUDE.md).</summary>
    public string GetServiceAppId(string serviceId)
        => throw new ServiceDoesNotExistException(
            $"Found service by key: {serviceId} yet no app id was provided.");
}
