using Microsoft.AspNetCore.Mvc.Filters;
using Numo.Common.Microservice.Lib.CurrentTenant.Setter;

namespace Numo.DevServices.Api.Features.ServiceData;

/// <summary>
/// Puts the caller's tenant id into the ambient tenant so the Numo client libraries send it
/// downstream. Attached to this slice's controller only: no other slice talks to those services.
/// </summary>
internal sealed class TenantIdActionFilter(INumoTenantSetterService tenantSetter) : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var headerValue = context.HttpContext.Request.Headers[ServiceDataRegistration.TenantIdHeaderName].ToString();

        // Short-circuited rather than defaulted: without a tenant the downstream call would either
        // fail obscurely or read some other tenant's data.
        if (!Guid.TryParse(headerValue, out var tenantId) || tenantId == Guid.Empty)
        {
            context.Result = new BadRequestObjectResult(ToProblemDetails(ServiceDataErrors.TenantIdMissing()));
            return;
        }

        tenantSetter.SetCurrentTenantId(tenantId);
    }

    /// <summary>
    /// A short-circuited filter never reaches the NumoResult-to-HTTP filter, so the failure is
    /// written in that filter's shape by hand: callers branch on <c>errorId</c> for every other
    /// failure of this slice and must not need a second rule for this one.
    /// </summary>
    private static ProblemDetails ToProblemDetails(NumoError error)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = error.Message,
            Detail = error.Message,
        };

        problemDetails.Extensions["errorId"] = error.Id;
        problemDetails.Extensions["message"] = error.Message;
        problemDetails.Extensions["errors"] = new[] { new { id = error.Id, message = error.Message } };

        return problemDetails;
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
