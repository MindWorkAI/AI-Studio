using System.Globalization;
using System.Text.RegularExpressions;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Keeps the names of presets and of comparison series unique, so that the user can tell them apart in their lists.
/// </summary>
/// <remarks>
/// Two names count as the same regardless of case and of spaces around them, as the user reads them.
/// </remarks>
public static partial class ModelComparisonNames
{
    /// <summary>
    /// Tells whether a name is taken already.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="takenNames">The names taken by the others, without the one the name belongs to.</param>
    /// <returns>True when another one has the name already.</returns>
    public static bool IsTaken(string name, IEnumerable<string> takenNames)
    {
        var trimmedName = name.Trim();
        return takenNames.Any(takenName => string.Equals(takenName.Trim(), trimmedName, StringComparison.CurrentCultureIgnoreCase));
    }

    /// <summary>
    /// Gets a name which is not taken yet, based on a given one: the name itself when it is free, otherwise the name
    /// with the next free number, e.g., "Weekly reports (2)".
    /// </summary>
    /// <param name="name">The name, e.g., of the series whose setup a new one takes over.</param>
    /// <param name="takenNames">The names taken already.</param>
    /// <param name="maxLength">How long the name may be; the base name is shortened for the number when needed.</param>
    /// <returns>The free name.</returns>
    public static string FreeNameFrom(string name, IReadOnlyCollection<string> takenNames, int maxLength)
    {
        var trimmedName = name.Trim();
        if (!IsTaken(trimmedName, takenNames) && trimmedName.Length <= maxLength)
            return trimmedName;

        // A name with a number already counts on from its base, so that a copy of "Reports (2)" becomes "Reports (3)":
        var match = NumberedName().Match(trimmedName);
        var baseName = match.Success ? match.Groups["base"].Value : trimmedName;
        for (var number = 2;; number++)
        {
            var suffix = string.Format(CultureInfo.InvariantCulture, " ({0})", number);
            var shortenedBase = baseName.Length + suffix.Length > maxLength ? baseName[..Math.Max(0, maxLength - suffix.Length)].TrimEnd() : baseName;
            var candidate = shortenedBase + suffix;
            if (!IsTaken(candidate, takenNames))
                return candidate;
        }
    }

    [GeneratedRegex(@"^(?<base>.+?) \((?<number>\d+)\)$")]
    private static partial Regex NumberedName();
}