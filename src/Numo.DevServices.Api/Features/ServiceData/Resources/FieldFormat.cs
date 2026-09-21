using System.Globalization;

namespace Numo.DevServices.Api.Features.ServiceData.Resources;

/// <summary>
/// How a DTO value becomes the string a <see cref="Cell"/> or a <see cref="FieldValue"/> carries.
/// Shared because the enum overloads carry a property no copy of them can be trusted to keep: the
/// numeric formatter is named apart and the enum ones are constrained, so a library version that
/// retypes an enum property as an integer stops compiling here instead of quietly rendering a number.
/// Invariant throughout, because these strings are read by a tool and not by a locale.
/// </summary>
internal static class FieldFormat
{
    public static string Format(bool value)
        => value ? "true" : "false";

    public static string? Format(bool? value)
        => value is null ? null : Format(value.Value);

    public static string Format(DateOnly value)
        => value.ToString("O", CultureInfo.InvariantCulture);

    public static string? Format(DateOnly? value)
        => value is null ? null : Format(value.Value);

    public static string? Format(DateTimeOffset? value)
        => value?.ToString("O", CultureInfo.InvariantCulture);

    /// <summary>Deliberately not called Format: an int widens to float implicitly, so a Format
    /// overload taking one would swallow an enum property a future library version retyped as an
    /// integer and render the number. Named apart, that case stops compiling.</summary>
    public static string FormatNumber(float value)
        => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The enums cross the wire as integers and the DTOs type them as enums, so this renders a name
    /// rather than a number without a lookup table of our own. Two overloads because C# will not
    /// lift a non-nullable argument into a <c>T?</c> parameter.
    /// </summary>
    public static string Format<T>(T value)
        where T : struct, Enum
        => value.ToString();

    public static string? Format<T>(T? value)
        where T : struct, Enum
        => value is null ? null : Format(value.Value);

    /// <summary>The closed list of an enum filter's values, so a UI can offer it and the page
    /// validator can reject anything else.</summary>
    public static IReadOnlyList<string> Options<T>()
        where T : struct, Enum
        => Enum.GetNames<T>();
}
