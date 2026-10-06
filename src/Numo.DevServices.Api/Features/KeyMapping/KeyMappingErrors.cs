namespace Numo.DevServices.Api.Features.KeyMapping;

// Stable ids let callers branch on a specific failure instead of parsing messages.
internal static class KeyMappingErrors
{
    private static readonly Guid ConnectorNotAttachedId = new("5c8e2a41-7d93-4b06-a1f5-3e9b7c2d8f14");
    private static readonly Guid KeyFieldValueMissingId = new("a73f1d58-2c6e-4e9a-8b47-0d5f9c1e6a32");
    private static readonly Guid DownstreamCallFailedId = new("e2b94c07-6a1d-4f38-9c5e-7b0a3d8f1e69");

    /// <summary>The service answers this case with an empty result instead of an error, so it is
    /// checked here before the conversion is sent.</summary>
    public static NumoError ConnectorNotAttached(Guid resourceId, string connectorName)
        => new(
            ConnectorNotAttachedId,
            $"Connector '{connectorName}' is not attached to resource {resourceId}, or has no key fields.");

    /// <summary>The service answers this case with an empty result instead of an error, so it is
    /// checked here before the conversion is sent.</summary>
    public static NumoError KeyFieldValueMissing(int rowNumber, IReadOnlyList<string> keyFields)
        => new(
            KeyFieldValueMissingId,
            $"Connector key on row {rowNumber} needs a non-blank value for each key field: {string.Join(", ", keyFields)}.");

    public static NumoError DownstreamCallFailed(string callDescription, int? statusCode)
        => new(
            DownstreamCallFailedId,
            statusCode is null
                ? $"The Configuration API call for {callDescription} failed."
                : $"The Configuration API call for {callDescription} failed with status {statusCode}.");
}
