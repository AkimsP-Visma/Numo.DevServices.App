namespace Numo.DevServices.Api.Features.Services;

// Stable ids let callers branch on a specific failure instead of parsing messages.
internal static class ServicesErrors
{
    private static readonly Guid UnknownServiceId = new("8a3d1c74-5e2b-4f18-9a6d-1c7e4b2f9d05");
    private static readonly Guid OpenApiUnavailableId = new("c4e91b06-2f7d-4a53-8b1c-6d9f3a5e7c18");

    public static NumoError UnknownService(string serviceName)
        => new(UnknownServiceId, $"Service '{serviceName}' is not registered in the Services configuration section.");

    public static NumoError OpenApiUnavailable(string serviceName, string reason)
        => new(OpenApiUnavailableId, $"Could not read the OpenAPI specification of '{serviceName}': {reason}");
}
