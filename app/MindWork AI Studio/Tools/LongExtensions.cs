namespace AIStudio.Tools;

public static class LongExtensions
{
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