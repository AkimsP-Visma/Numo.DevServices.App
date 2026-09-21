namespace Numo.DevServices.Api.Features.FeatureFlags;

/// <summary>
/// Bound to the same section as Numo.Common.Lib's own LaunchDarkly options so that every
/// LaunchDarkly credential sits in one place. That library's SdkKey cannot serve this feature:
/// the server SDK only evaluates a named flag for a context and never receives names or tags.
/// </summary>
public sealed class LaunchDarklyApiOptions
{
    public const string ConfigurationSectionName = "FeatureFlagOptions:LaunchDarkly";

    /// <summary>API access token, sent verbatim as the Authorization header.</summary>
    public string? ApiToken { get; set; }

    /// <summary>Key of the project whose flags are listed.</summary>
    public string? ProjectKey { get; set; }
}
