using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Numo.DevServices.Api.Features.FeatureFlags;

// Only the parts of the LaunchDarkly API responses this feature reads.

internal sealed record LaunchDarklyEnvironmentsResponse
{
    public IReadOnlyList<LaunchDarklyEnvironment> Items { get; init; } = [];
}

internal sealed record LaunchDarklyEnvironment
{
    public string Key { get; init; } = string.Empty;
}

internal sealed record LaunchDarklyFlagsResponse
{
    public IReadOnlyList<LaunchDarklyFlag> Items { get; init; } = [];
}

internal sealed record LaunchDarklyFlag
{
    public string Key { get; init; } = string.Empty;

    public string? Name { get; init; }

    public string? Description { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public bool Archived { get; init; }

    public IReadOnlyList<LaunchDarklyVariation> Variations { get; init; } = [];

    // Present only for the environments named by an "env" query parameter.
    public IReadOnlyDictionary<string, LaunchDarklyEnvironmentConfiguration> Environments { get; init; }
        = new Dictionary<string, LaunchDarklyEnvironmentConfiguration>();
}

internal sealed record LaunchDarklyVariation
{
    public JsonNode? Value { get; init; }
}

internal sealed record LaunchDarklyEnvironmentConfiguration
{
    public bool On { get; init; }

    [JsonPropertyName("_summary")]
    public LaunchDarklyEnvironmentSummary? Summary { get; init; }
}

/// <summary>
/// Keyed by variation index as a string. The summary is what names the served variation: the
/// full <c>fallthrough</c> and <c>offVariation</c> fields only appear under <c>summary=0</c>,
/// which also drags in every targeting rule.
/// </summary>
internal sealed record LaunchDarklyEnvironmentSummary
{
    public IReadOnlyDictionary<string, LaunchDarklyVariationSummary> Variations { get; init; }
        = new Dictionary<string, LaunchDarklyVariationSummary>();
}

internal sealed record LaunchDarklyVariationSummary
{
    public bool IsFallthrough { get; init; }

    public bool IsOff { get; init; }
}
