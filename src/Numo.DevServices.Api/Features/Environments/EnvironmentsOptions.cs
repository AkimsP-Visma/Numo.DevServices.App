namespace Numo.DevServices.Api.Features.Environments;

/// <summary>
/// Bound from the "Environments" configuration section: a set of named environments, each with its
/// own copy of the service locations that "Services" used to hold flat. None of this is secret - it
/// is the same plain hostnames the old section held, just three (or four, counting Local) sets
/// instead of one - so it lives in the committed appsettings.json, not the gitignored Development
/// file.
/// </summary>
public sealed class EnvironmentsOptions
{
    public const string ConfigurationSectionName = "Environments";

    public string Default { get; set; } = "";

    public Dictionary<string, EnvironmentDefinition> Definitions { get; set; } = new();
}

public sealed class EnvironmentDefinition
{
    public Dictionary<string, ServiceLocationDefinition> Services { get; set; } = new();
}

/// <summary>Mirrors the shape Numo.Common.Lib's own ConfigurationServiceDiscoveryService reads from a
/// flat "Services" section - same two properties, same names.</summary>
public sealed class ServiceLocationDefinition
{
    public string? Location { get; set; }

    public string? AppId { get; set; }
}
