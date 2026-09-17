using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools;

public static class LongExtensions
{
    private static readonly string[] COUNT_SUFFIXES = ["k", "M", "B", "T"];

    /// <summary>
    /// Formats a count so that large numbers stay readable: 1456 becomes 1.46k, 4512900 becomes 4.51M.
    /// </summary>
    /// <remarks>
    /// Counts scale in thousands, not in steps of 1024 — that is what FileSize is for, and storage
    /// sizes keep using it. Numbers below 1000 stay exact, because shortening them would hide the
    /// difference between 4 and 999. The decimal separator follows the culture of the active language
    /// plugin rather than the one of the thread, which never moves along with the app's language.
    /// </remarks>
    /// <param name="count">The number to format.</param>
    /// <returns>The formatted number.</returns>
    public static string CompactCount(this long count)
    {
        var culture = I18N.I.Culture;
        if (count is > -1_000 and < 1_000)
            return count.ToString("N0", culture);

        var order = -1;
        double value = count;
        while (Math.Abs(value) >= 1_000 && order < COUNT_SUFFIXES.Length - 1)
        {
            order++;
            value /= 1_000;
        }

        return $"{value.ToString("0.##", culture)}{COUNT_SUFFIXES[order]}";
    }

    /// <summary>
    /// Formats a count so that large numbers stay readable.
    /// </summary>
    /// <param name="count">The number to format.</param>
    /// <returns>The formatted number.</returns>
    public static string CompactCount(this int count) => ((long)count).CompactCount();

    /// <summary>
    /// Formats the file size in a human-readable format.
    /// </summary>
    /// <param name="sizeBytes">The size in bytes.</param>
    /// <returns>The formatted file size.</returns>
    public static string FileSize(this long sizeBytes)
    {
        string[] sizes = { "B", "kB", "MB", "GB", "TB" };
        var order = 0;
        while (sizeBytes >= 1024 && order < sizes.Length - 1)
        {
            order++;
            sizeBytes /= 1024;
        }

        return $"{sizeBytes:0.##} {sizes[order]}";
    }
}