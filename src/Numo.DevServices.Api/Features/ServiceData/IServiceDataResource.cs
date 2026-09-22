namespace Numo.DevServices.Api.Features.ServiceData;

/// <summary>
/// One browsable resource of one Numo service. An implementation may issue several downstream calls
/// per method: employees and positions need a person lookup to show a name at all.
/// Failures leave through <see cref="DownstreamCallException"/>, which keeps the client libraries'
/// two different failure styles - a throw and a FluentResults result - off the wire contract.
/// </summary>
public interface IServiceDataResource
{
    ResourceDescriptor Descriptor { get; }

    Task<ResourcePage> GetPageAsync(ResourceQuery query, CancellationToken cancellationToken);

    /// <param name="filters">The caller's currently active filters, keyed by
    /// <see cref="FilterDescriptor.Key"/>. Every flat resource ignores this: it exists so a nested
    /// resource's detail route can read the parent id its route needs (di-client-resources and its
    /// siblings), which the bare id alone cannot carry.</param>
    /// <returns>Null when the service has no record with that id.</returns>
    Task<ResourceRecord?> GetByIdAsync(
        Guid id,
        IReadOnlyDictionary<string, string> filters,
        CancellationToken cancellationToken);
}
