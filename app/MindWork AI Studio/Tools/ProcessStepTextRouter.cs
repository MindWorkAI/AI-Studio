using System.Text;

using AIStudio.Components;

namespace AIStudio.Tools;

/// <summary>
/// Routes process step enums to their corresponding text representations, when possible.
/// </summary>
public static class ProcessStepTextRouter
{
    /// <summary>
    /// Gets the text representation of a given process step enum.
    /// </summary>
    /// <remarks>
    /// Gets the text representation of a given process step enum.
    /// When the enum type has a specific extension method for text retrieval, it uses that;
    /// otherwise, it derives a name based on the enum value.
    /// </remarks>
    /// <param name="step">The process step enum value.</param>
    /// <typeparam name="T">The enum type representing the process steps.</typeparam>
    /// <returns>The text representation of the process step.</returns>
    public static string GetText<T>(T step) where T : struct, Enum => step switch
    {
        ReadWebContentSteps x => x.GetText(),
        _ => DeriveName(step)
    };

    /// <summary>
    /// Derives a name from the enum value by converting it to a more human-readable format.
    /// It handles both single-word and multi-word enum values (separated by underscores).
    /// </summary>
    /// <param name="value">The enum value to derive the name from.</param>
    /// <typeparam name="T">The enum type.</typeparam>
    /// <returns>A human-readable name derived from the enum value.</returns>
    private static string DeriveName<T>(T value) where T : struct, Enum
    {
        var text = value.ToString();
        if (!text.Contains('_'))
        {
            text = text.ToLowerInvariant();
            text = char.ToUpperInvariant(text[0]) + text[1..];
        }
        else
        {
            var parts = text.Split('_');
            var sb = new StringBuilder();
            foreach (var part in parts)
            {
                sb.Append(char.ToUpperInvariant(part[0]));
                sb.Append(part[1..].ToLowerInvariant());
            }
            
            text = sb.ToString();
        }
        
        return text;
    }
}