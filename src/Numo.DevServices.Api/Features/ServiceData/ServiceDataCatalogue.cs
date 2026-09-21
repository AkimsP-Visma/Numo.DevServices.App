namespace Numo.DevServices.Api.Features.ServiceData;

/// <summary>
/// The only place a resource key becomes a resource. The set is closed at compile time by what is
/// registered, so a route segment cannot name anything that is not a registered resource.
/// </summary>
public sealed class ServiceDataCatalogue
{
    private readonly IReadOnlyDictionary<string, IServiceDataResource> resourcesByKey;

    public ServiceDataCatalogue(IEnumerable<IServiceDataResource> resources)
    {
        resourcesByKey = resources.ToDictionary(
            resource => resource.Descriptor.Key,
            StringComparer.OrdinalIgnoreCase);

        Descriptors = resourcesByKey.Values
            .Select(resource => resource.Descriptor)
            .OrderBy(descriptor => descriptor.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<ResourceDescriptor> Descriptors { get; }

    public IServiceDataResource? Find(string key)
        => resourcesByKey.GetValueOrDefault(key);
}
