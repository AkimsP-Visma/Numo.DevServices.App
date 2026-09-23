namespace Numo.DevServices.Api.Features.Environments;

internal static class EnvironmentsErrors
{
    private static readonly Guid UnknownEnvironmentId = new("2b6f8e14-9c3a-4d57-8b21-6e4f0a7d5c93");

    public static NumoError UnknownEnvironment(string environmentKey)
        => new(UnknownEnvironmentId, $"'{environmentKey}' is not a configured environment.");
}
