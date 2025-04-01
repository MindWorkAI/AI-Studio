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
}