namespace Numo.DevServices.Api.Features.Environments;

public sealed record GetCurrentEnvironmentQuery;

// The mediator fails the request outright when a request type has no validator, so a rule-less one is required.
public sealed class GetCurrentEnvironmentValidator : AbstractValidator<GetCurrentEnvironmentQuery>;

/// <param name="Current">The active environment key.</param>
/// <param name="Known">Every configured environment key, in the order appsettings declares them -
/// what the frontend selector offers.</param>
public sealed record GetCurrentEnvironmentResult(string Current, IReadOnlyList<string> Known);

public sealed class GetCurrentEnvironmentHandler(CurrentEnvironmentStore store)
{
    public Task<NumoResult<GetCurrentEnvironmentResult>> HandleAsync(
        GetCurrentEnvironmentQuery query,
        CancellationToken cancellationToken)
        => Task.FromResult(
            NumoResult.Ok(new GetCurrentEnvironmentResult(store.Current, store.Known)));
}
