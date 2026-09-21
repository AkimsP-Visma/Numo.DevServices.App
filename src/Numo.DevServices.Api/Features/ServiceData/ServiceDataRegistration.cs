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

        // The catalogue's resources are not registered here: numo-core's convention scan over this
        // assembly already registers every class against the interfaces it implements, so an
        // explicit IServiceDataResource registration makes the catalogue see the same resource twice.
        services.AddScoped<ServiceDataCatalogue>();

        // The convention scan registers a class against the interfaces it implements, and this one
        // implements none, so without this line every resource that resolves it fails to activate.
        services.AddScoped<PersonNameLookup>();

        return services;
    }
}
