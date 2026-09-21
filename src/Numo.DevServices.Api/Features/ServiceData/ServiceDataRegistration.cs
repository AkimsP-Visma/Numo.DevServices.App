using Numo.Common.Microservice.Lib.CurrentTenant;
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

        return services;
    }
}
