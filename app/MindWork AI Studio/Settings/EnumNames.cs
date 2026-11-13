using System.Diagnostics.CodeAnalysis;

namespace AIStudio.Settings;

/// <summary>
/// Reads the name of an enum member, and nothing else.
/// </summary>
/// <remarks>
/// For values somebody wrote by hand, such as those of a configuration plugin. Enum.TryParse
/// accepts more than a name: a number, which becomes whichever member has that value or a value
/// no member has, and several names separated by commas, which it combines bit by bit as if the
/// enum were a set of flags. The enums read from a configuration are no such sets, so
/// "SEMANTIC_SEARCH, EVERY_MESSAGE" would quietly become EVERY_MESSAGE, and "HIGH, MEDIUM" a
/// confidence level which no provider reaches. Only a single name counts here, ignoring case and
/// surrounding white space as Enum.TryParse does.
/// </remarks>
public static class EnumNames
{
    /// <typeparam name="TEnum">The enum to read a member of.</typeparam>
    extension<TEnum>(TEnum) where TEnum : struct, Enum
    {
        /// <summary>
        /// Reads the name of a member of the enum.
        /// </summary>
        /// <remarks>
        /// Named TryParseName rather than TryParse: Enum.TryParse is reached through every enum as
        /// well, and C# prefers it over an extension member of the same name. Code which knows the
        /// enum only as a type parameter writes EnumNames.TryParseName&lt;TEnum&gt;(…), since C# looks
        /// up no static member on a type parameter.
        /// </remarks>
        /// <param name="text">The text to read.</param>
        /// <param name="value">The member named, or the default when the text names none.</param>
        /// <returns>True when the text names exactly one member.</returns>
        public static bool TryParseName(string? text, out TEnum value)
        {
            if (TryParse(typeof(TEnum), text, out var member))
            {
                value = (TEnum)member;
                return true;
            }

            value = default;
            return false;
        }
    }

    /// <summary>
    /// Reads the name of a member of the given enum type.
    /// </summary>
    /// <param name="enumType">The enum type to read a member of.</param>
    /// <param name="text">The text to read.</param>
    /// <param name="value">The member named, or null when the text names none.</param>
    /// <returns>True when the text names exactly one member.</returns>
    public static bool TryParse(Type enumType, string? text, [NotNullWhen(true)] out object? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmedText = text.Trim();
        var name = Enum.GetNames(enumType).FirstOrDefault(candidate => candidate.Equals(trimmedText, StringComparison.OrdinalIgnoreCase));
        if (name is null)
            return false;

        value = Enum.Parse(enumType, name);
        return true;
    }
}