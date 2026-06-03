using System.Globalization;
using System.Text;

namespace AIStudio.Tools;

public static class CommonTools
{
    /// <summary>
    /// Get all the values (the names) of an enum as a string, separated by commas.
    /// </summary>
    /// <typeparam name="TEnum">The enum type to get the values of.</typeparam>
    /// <param name="exceptions">The values to exclude from the result.</param>
    /// <returns>The values of the enum as a string, separated by commas.</returns>
    public static string GetAllEnumValues<TEnum>(params TEnum[] exceptions) where TEnum : struct, Enum
    {
        var sb = new StringBuilder();
        foreach (var value in Enum.GetValues<TEnum>())
            if(!exceptions.Contains(value))
                sb.Append(value).Append(", ");
        
        return sb.ToString();
    }

    /// <summary>
    /// Resolves a <see cref="CultureInfo"/> from the active language plugin's IETF tag.
    /// </summary>
    /// <param name="ietfTag">The IETF language tag provided by the active language plugin.</param>
    /// <returns>The matching culture when the tag is valid; otherwise <see cref="CultureInfo.InvariantCulture"/>.</returns>
    public static CultureInfo DeriveActiveCultureOrInvariant(string? ietfTag)
    {
        if (string.IsNullOrWhiteSpace(ietfTag))
            return CultureInfo.InvariantCulture;

        try
        {
            return CultureInfo.GetCultureInfo(ietfTag);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
    
    /// <summary>
    /// Formats a timestamp using the short date and time pattern of the specified culture.
    /// </summary>
    /// <param name="timestamp">The timestamp to format.</param>
    /// <param name="culture">The culture whose short date and time pattern should be used.</param>
    /// <returns>The localized timestamp string.</returns>
    public static string FormatTimestampToGeneral(DateTime timestamp, CultureInfo culture) => timestamp.ToString("g", culture);
}
