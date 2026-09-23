namespace Numo.DevServices.Api.Features.Environments;

public sealed record SetEnvironmentCommand(string EnvironmentKey);

public sealed class SetEnvironmentValidator : AbstractValidator<SetEnvironmentCommand>
{
    public SetEnvironmentValidator()
    {
        RuleFor(command => command.EnvironmentKey).NotEmpty();
    }
}

public sealed record SetEnvironmentResult(string Current);

/// <summary>Membership in the known set is checked here, not in the validator: like
/// GetServiceDataPageValidator's unknown-resource handling, a lookup needs CurrentEnvironmentStore
/// anyway, so the handler is where "unknown" becomes a stable, branchable failure.</summary>
public sealed class SetEnvironmentHandler(CurrentEnvironmentStore store)
{
    public Task<NumoResult<SetEnvironmentResult>> HandleAsync(
        SetEnvironmentCommand command,
        CancellationToken cancellationToken)
    {
        if (!store.TrySet(command.EnvironmentKey))
        {
            return Task.FromResult(
                NumoResult.Fail<SetEnvironmentResult>(EnvironmentsErrors.UnknownEnvironment(command.EnvironmentKey)));
        }

        return Task.FromResult(NumoResult.Ok(new SetEnvironmentResult(store.Current)));
    }
}
