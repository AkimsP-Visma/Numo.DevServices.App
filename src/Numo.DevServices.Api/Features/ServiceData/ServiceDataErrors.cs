namespace Numo.DevServices.Api.Features.ServiceData;

// Stable ids let callers branch on a specific failure instead of parsing messages.
internal static class ServiceDataErrors
{
    private static readonly Guid UnknownResourceId = new("6f1b9c2e-4d83-4a17-9b5c-2e7a8d40f913");
    private static readonly Guid TenantIdMissingId = new("b0c74e15-93af-4d26-8f31-5a6b2c9e7d04");
    private static readonly Guid RecordNotFoundId = new("d38f5a62-7c14-4e9b-86d0-3f1b7e2a95c8");
    private static readonly Guid DownstreamCallFailedId = new("1e5a9d37-2b6c-4f80-9d14-7c3e8b5f2a60");
    private static readonly Guid DownstreamCallUnauthorizedId = new("47c2e8b9-5f01-4a3d-92b6-8d7e1c4a06f5");

    public static NumoError UnknownResource(string resourceKey)
        => new(UnknownResourceId, $"Service data resource '{resourceKey}' does not exist.");

    public static NumoError TenantIdMissing()
        => new(
            TenantIdMissingId,
            $"The '{ServiceDataRegistration.TenantIdHeaderName}' request header must carry a tenant id in GUID form.");

    public static NumoError RecordNotFound(string resourceKey, Guid id)
        => new(RecordNotFoundId, $"Resource '{resourceKey}' has no record with id {id}.");

    /// <param name="statusCode">Absent when the failure carried no HTTP status, which is the common
    /// case: the client libraries report a service-side failure as an exception without one.</param>
    public static NumoError DownstreamCallFailed(string callDescription, int? statusCode)
        => new(
            DownstreamCallFailedId,
            statusCode is null
                ? $"The downstream call for {callDescription} failed."
                : $"The downstream call for {callDescription} failed with status {statusCode}.");

    /// <summary>
    /// Separate from <see cref="DownstreamCallFailed"/> because the client libraries throw
    /// client-side, before a request is sent, when the app has no authenticated principal and the
    /// AllowUnauthorizedApiCalls toggle is off. There is no status code to report.
    /// </summary>
    public static NumoError DownstreamCallUnauthorized(string callDescription)
        => new(
            DownstreamCallUnauthorizedId,
            $"The downstream call for {callDescription} was rejected before it was sent because this app has no authenticated principal.");
}
