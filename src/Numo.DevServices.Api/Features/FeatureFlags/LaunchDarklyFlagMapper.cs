using System.Globalization;
using System.Text.Json.Nodes;

namespace Numo.DevServices.Api.Features.FeatureFlags;

/// <summary>
/// Turns LaunchDarkly's flag documents into the environment-per-column shape the page renders.
/// </summary>
internal static class LaunchDarklyFlagMapper
{
    public static GetFeatureFlagsResult ToResult(
        IReadOnlyList<string> environmentKeys,
        IReadOnlyList<LaunchDarklyFlag> flags)
    {
        IReadOnlyList<FeatureFlagRow> rows = flags
            .Where(flag => !flag.Archived)
            .OrderBy(flag => flag.Key, StringComparer.Ordinal)
            .Select(ToRow)
            .ToList();

        return new GetFeatureFlagsResult(environmentKeys, rows);
    }

    private static FeatureFlagRow ToRow(LaunchDarklyFlag flag)
        => new(
            flag.Key,
            // A flag created through the API can be nameless, and then the key is the only label.
            string.IsNullOrWhiteSpace(flag.Name) ? flag.Key : flag.Name,
            flag.Description,
            flag.Tags,
            flag.Environments.ToDictionary(
                environment => environment.Key,
                environment => ToEnvironmentState(flag, environment.Value),
                StringComparer.Ordinal));

    private static FeatureFlagEnvironmentState ToEnvironmentState(
        LaunchDarklyFlag flag,
        LaunchDarklyEnvironmentConfiguration configuration)
    {
        var servedIndexes = GetServedVariationIndexes(configuration);

        // Several fallthrough variations at once mean a percentage split, where no one value applies.
        var isRollout = configuration.On && servedIndexes.Count > 1;

        return new FeatureFlagEnvironmentState(
            configuration.On,
            isRollout ? null : GetVariationValue(flag, servedIndexes.Count > 0 ? servedIndexes[0] : -1),
            isRollout);
    }

    /// <summary>
    /// The summary marks which variation the default rule serves and which one an off flag serves;
    /// one variation can carry both marks.
    /// </summary>
    private static List<int> GetServedVariationIndexes(LaunchDarklyEnvironmentConfiguration configuration)
    {
        var variations = configuration.Summary?.Variations;

        if (variations is null)
        {
            return [];
        }

        return variations
            .Where(variation => configuration.On ? variation.Value.IsFallthrough : variation.Value.IsOff)
            .Select(variation => int.TryParse(variation.Key, CultureInfo.InvariantCulture, out var index) ? index : -1)
            .Where(index => index >= 0)
            .OrderBy(index => index)
            .ToList();
    }

    /// <summary>
    /// A missing index is normal rather than an error: a flag with no variation marked for its
    /// current state serves whatever default the calling SDK passes in, which is unknowable here.
    /// </summary>
    private static JsonNode? GetVariationValue(LaunchDarklyFlag flag, int variationIndex)
        => variationIndex >= 0 && variationIndex < flag.Variations.Count
            ? flag.Variations[variationIndex].Value
            : null;
}
