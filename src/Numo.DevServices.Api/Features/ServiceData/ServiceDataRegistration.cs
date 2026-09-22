using Numo.Common.Lib.ServiceDiscovery;
using Numo.Common.Microservice.Lib.CurrentTenant;
using Numo.DevServices.Api.Features.ServiceData.Resources;
using Numo.Employee.Lib.Extensions;
using Numo.Person.Lib.Extensions;

namespace Numo.DevServices.Api.Features.ServiceData;

internal static class ServiceDataRegistration
{
    /// <summary>
    /// The platform's own tenant header name, sent by the frontend and read by
    /// <see cref="TenantIdActionFilter"/>.
    /// </summary>
    public const string TenantIdHeaderName = "Numo-Tenant-Id";

    /// <summary>The client <see cref="Resources.DepartmentRolesResource"/> reads its route over. It is
    /// the slice's only hand-rolled downstream access; see that file for why it exists.</summary>
    public const string DepartmentRolesHttpClientName = "NumoEmployeeApiDepartmentRoles";

    /// <summary>The route that client reads, taken from the Employee service's own constant rather
    /// than spelled out again.</summary>
    public const string DepartmentRolesPath = Numo.Employee.Common.Constants.DepartmentRolesEndpointPath;

    /// <summary>The key the Employee service is configured under in the "Services" section.</summary>
    private const string EmployeeServiceDiscoveryKey = "Numo.Employee.Api";

    /// <summary>The client every DataIntegration resource reads over - see
    /// <see cref="Resources.DataIntegrationConfigurationApi"/> for why none of them use a client
    /// library. The key is the literal "Services" section entry, already present in both
    /// appsettings.json and the development template.</summary>
    public const string DataIntegrationConfigurationHttpClientName = "NumoDataIntegrationConfigurationApi";

    private const string DataIntegrationConfigurationServiceDiscoveryKey = "Numo.DataIntegration.Configuration.Api";

    private static readonly TimeSpan DepartmentRolesRequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DataIntegrationRequestTimeout = TimeSpan.FromSeconds(30);

    public static IServiceCollection AddServiceDataFeature(this IServiceCollection services)
    {
        // Read-only browsing: only the plain clients are registered. The Add*TestClient variants
        // expose Insert, Update and Delete, so registering one would open a write path to live data.
        // AddEmployeeClient registers the employee client alone, hence the five separate calls.
        services.AddPersonClient();
        services.AddEmployeeClient();
        services.AddPositionClient();
        services.AddJobTitleClient();
        services.AddDepartmentClient();
        services.AddAbsenceClient();

        // Each Add*Client above already calls AddNumoTenants. The setter is what lets a request
        // supply the tenant id, and it is scoped because the value belongs to one request.
        services.AddNumoTenantSetter(ServiceLifetime.Scoped);

        AddDepartmentRolesHttpClient(services);
        AddDataIntegrationConfigurationHttpClient(services);

        // The catalogue's resources are not registered here: numo-core's convention scan over this
        // assembly already registers every class against the interfaces it implements, so an
        // explicit IServiceDataResource registration makes the catalogue see the same resource twice.
        services.AddScoped<ServiceDataCatalogue>();

        // The convention scan registers a class against the interfaces it implements, and these
        // implement none, so without these lines every resource that resolves them fails to activate.
        services.AddScoped<PersonNameLookup>();
        services.AddScoped<DataIntegrationConfigurationApi>();

        return services;
    }

    /// <summary>
    /// Numo.DataIntegration.Configuration.Lib and Numo.DataIntegration.Connectors.Lib are
    /// deliberately not referenced at all: neither fits this section's browsing needs (see
    /// <see cref="Resources.DataIntegrationConfigurationApi"/>), so every resource here reads the
    /// Configuration API directly. No tenant header: a live probe confirmed the service answers
    /// anonymously.
    /// </summary>
    private static void AddDataIntegrationConfigurationHttpClient(IServiceCollection services)
        => services
            .AddHttpClient(DataIntegrationConfigurationHttpClientName)
            .ConfigureHttpClient((serviceProvider, httpClient) =>
            {
                httpClient.BaseAddress = serviceProvider
                    .GetRequiredService<IServiceDiscoveryService>()
                    .GetServiceLocation(DataIntegrationConfigurationServiceDiscoveryKey);
                httpClient.Timeout = DataIntegrationRequestTimeout;
            });

    /// <summary>
    /// The department roles resource has no client library to configure it, so its base address is
    /// read from service discovery here. Everything is assigned rather than accumulated, so that a
    /// stray second registration of this action overwrites the same values instead of compounding.
    /// The tenant header is not set here: it belongs to a request, not to the shared client.
    /// </summary>
    private static void AddDepartmentRolesHttpClient(IServiceCollection services)
        => services
            .AddHttpClient(DepartmentRolesHttpClientName)
            .ConfigureHttpClient((serviceProvider, httpClient) =>
            {
                httpClient.BaseAddress = serviceProvider
                    .GetRequiredService<IServiceDiscoveryService>()
                    .GetServiceLocation(EmployeeServiceDiscoveryKey);
                httpClient.Timeout = DepartmentRolesRequestTimeout;
            });
}
