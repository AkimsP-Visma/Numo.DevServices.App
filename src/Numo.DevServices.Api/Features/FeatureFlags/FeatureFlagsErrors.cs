namespace Numo.DevServices.Api.Features.FeatureFlags;

// Stable ids let callers branch on a specific failure instead of parsing messages.
internal static class FeatureFlagsErrors
{
    private static readonly Guid NotConfiguredId = new("2740ce41-1070-48b3-bc3b-f26fde0cd697");
    private static readonly Guid LaunchDarklyUnavailableId = new("11f73e5a-6bed-4c35-be23-37b7e3999dc0");

    public static NumoError NotConfigured()
        => new(
            NotConfiguredId,
            $"LaunchDarkly is not configured: '{LaunchDarklyApiOptions.ConfigurationSectionName}' needs both ApiToken and ProjectKey.");

    public static NumoError LaunchDarklyUnavailable(string reason)
        => new(LaunchDarklyUnavailableId, $"Could not read the feature flags from LaunchDarkly: {reason}");
}
