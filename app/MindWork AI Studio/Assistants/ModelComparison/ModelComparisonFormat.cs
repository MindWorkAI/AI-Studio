using System.Globalization;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Formats the numbers which the Model Comparison assistant shows, in the culture of the active language.
/// </summary>
public static class ModelComparisonFormat
{
    /// <summary>
    /// Gets the culture of the active language. Qualified with its namespace because the namespace of the localization
    /// assistant, AIStudio.Assistants.I18N, hides the class I18N from every assistant.
    /// </summary>
    public static CultureInfo Culture => Tools.PluginSystem.I18N.I.Culture;

    /// <summary>
    /// Formats seconds with one decimal place, e.g., "2.5 s".
    /// </summary>
    /// <param name="seconds">The seconds.</param>
    /// <returns>The seconds with their unit.</returns>
    public static string Seconds(double seconds) => $"{seconds.ToString("0.0", Culture)} s";

    /// <summary>
    /// Formats a duration in seconds.
    /// </summary>
    /// <param name="duration">The duration, or null when it is not known.</param>
    /// <returns>The seconds with their unit, or a dash.</returns>
    public static string Duration(TimeSpan? duration) => duration is { } value
        ? Seconds(value.TotalSeconds)
        : "-";

    /// <summary>
    /// Formats a share as a whole percentage.
    /// </summary>
    /// <param name="share">The share between 0 and 1, or null when it is not known.</param>
    /// <returns>The percentage, or a dash.</returns>
    public static string Share(double? share) => share is { } value
        ? value.ToString("P0", Culture)
        : "-";
}