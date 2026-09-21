using Microsoft.AspNetCore.Mvc.Filters;
using Numo.Common.Microservice.Lib.CurrentTenant.Setter;

namespace Numo.DevServices.Api.Features.ServiceData;

/// <summary>
/// Puts the caller's tenant id into the ambient tenant so the Numo client libraries send it
/// downstream. Attached to this slice's controller only: no other slice talks to those services.
/// </summary>
internal sealed class TenantIdActionFilter(INumoTenantSetterService tenantSetter) : IActionFilter
{
    private const string MissingTenantIdDetail =
        $"The '{ServiceDataRegistration.TenantIdHeaderName}' request header must carry a tenant id in GUID form.";

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var headerValue = context.HttpContext.Request.Headers[ServiceDataRegistration.TenantIdHeaderName].ToString();

        // Short-circuited rather than defaulted: without a tenant the downstream call would either
        // fail obscurely or read some other tenant's data.
        if (!Guid.TryParse(headerValue, out var tenantId) || tenantId == Guid.Empty)
        {
            context.Result = new BadRequestObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Tenant id missing",
                Detail = MissingTenantIdDetail,
            });

            return;
        }

        tenantSetter.SetCurrentTenantId(tenantId);
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
