using Microsoft.Extensions.Options;

namespace Numo.DevServices.Api.Features.Environments;

/// <summary>
/// The one place the active environment key lives: in memory, for the app's whole lifetime, reset
/// to <see cref="EnvironmentsOptions.Default"/> on every restart. No persistence, the same ephemeral
/// philosophy as the frontend's TenantIdStore - a developer picks it again after a restart rather
/// than the app carrying a stale choice across runs.
/// </summary>
public sealed class CurrentEnvironmentStore
{
    private readonly IOptions<EnvironmentsOptions> _options;
    private readonly Lock _lock = new();
    private string _current;

    public CurrentEnvironmentStore(IOptions<EnvironmentsOptions> options)
    {
        _options = options;
        _current = options.Value.Default;
    }

    public string Current
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    public IReadOnlyList<string> Known => _options.Value.Definitions.Keys.ToList();

    public bool IsKnown(string environmentKey) => _options.Value.Definitions.ContainsKey(environmentKey);

    /// <returns>false when <paramref name="environmentKey"/> is not one of the configured
    /// environments - the caller decides what that means (a validation failure, typically).</returns>
    public bool TrySet(string environmentKey)
    {
        if (!IsKnown(environmentKey))
        {
            return false;
        }

        lock (_lock)
        {
            _current = environmentKey;
        }

        return true;
    }

    public EnvironmentDefinition CurrentDefinition => _options.Value.Definitions[Current];
}
