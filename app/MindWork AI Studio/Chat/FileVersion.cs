namespace AIStudio.Chat;

/// <summary>
/// The state of a file which a text was read from, to tell whether the file changed since.
/// </summary>
/// <remarks>
/// The size counts as well as the time: some file systems keep the time coarsely, e.g., FAT to two
/// seconds, and a network drive may report the time of another clock.
/// </remarks>
/// <param name="LastWriteUtc">When the file was last written.</param>
/// <param name="Length">The size of the file in bytes.</param>
public sealed record FileVersion(DateTime LastWriteUtc, long Length)
{
    /// <summary>
    /// Reads the state of a file as it is now.
    /// </summary>
    /// <param name="filePath">The path of the file.</param>
    /// <returns>The state, or null when the file is gone.</returns>
    public static FileVersion? Of(string filePath)
    {
        var file = new FileInfo(filePath);
        return file.Exists ? new(file.LastWriteTimeUtc, file.Length) : null;
    }
}