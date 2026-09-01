namespace Numo.DevServices.Api.Features.SampleItems;

// Stable ids let callers branch on a specific failure instead of parsing messages.
internal static class SampleItemErrors
{
    private static readonly Guid NotFoundId = new("6f6f6e21-0d5a-4a2f-9c4e-2f4f3b8f6a11");
    private static readonly Guid NameAlreadyUsedId = new("2b1f4c6e-70ad-4f19-8d3f-9a6c1e5b4d22");

    public static NumoError NotFound(Guid id)
        => new(NotFoundId, $"Sample item '{id}' was not found.");

    public static NumoError NameAlreadyUsed(string name)
        => new(NameAlreadyUsedId, $"A sample item named '{name}' already exists.");
}
