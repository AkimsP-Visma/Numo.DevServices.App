namespace Numo.DevServices.Api.Features.KeyMapping;

public sealed record GetKeyMappingClientsQuery;

// The mediator fails the request outright when a request type has no validator, so a rule-less one is required.
public sealed class GetKeyMappingClientsValidator : AbstractValidator<GetKeyMappingClientsQuery>;

public sealed class GetKeyMappingClientsHandler(ConfigurationKeysApi api)
{
    public async Task<NumoResult<IReadOnlyList<KeyMappingOption>>> HandleAsync(
        GetKeyMappingClientsQuery query,
        CancellationToken cancellationToken)
    {
        var clients = await api.GetAsync<List<ConfigurationClient>>("api/clients", "the client list", cancellationToken);

        return clients.IsFailed
            ? NumoResult.Fail<IReadOnlyList<KeyMappingOption>>(clients.Errors)
            : NumoResult.Ok(KeyMappingOptions.From(clients.Value.Select(client => (client.Id, client.Name))));
    }
}

internal static class KeyMappingOptions
{
    /// <summary>An unnamed record still needs a readable label, and its id is the only one it has.</summary>
    public static IReadOnlyList<KeyMappingOption> From(IEnumerable<(Guid Id, string? Name)> records)
        => records
            .Select(record => new KeyMappingOption(
                record.Id,
                string.IsNullOrWhiteSpace(record.Name) ? record.Id.ToString() : record.Name))
            .OrderBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
