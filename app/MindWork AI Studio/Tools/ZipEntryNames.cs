namespace AIStudio.Tools;

/// <summary>
/// Checks the names of entries in ZIP files before anything is written to the disk.
/// </summary>
/// <remarks>
/// A ZIP file comes from somewhere else: a plugin archive, the configuration of an organization, or a
/// backup. An entry name such as "../settings.json" or an absolute path would make extracting it
/// write outside of the target directory (zip slip). Every name is therefore checked here before it
/// becomes a path.<br/><br/>
///
/// Backslashes count as separators as well. Compatibility shim for ZIP files created on Windows
/// with backslashes in their entry names (dotnet/runtime#27620); remove after dotnet/runtime#27620
/// and #41914 are fixed. See documentation/compatibility-shims/2026-07-plugin-archive-zip-backslashes.md.
/// </remarks>
public static class ZipEntryNames
{
    /// <summary>
    /// The separator of entry names, which the ZIP format prescribes.
    /// </summary>
    public const char SEPARATOR = '/';

    /// <summary>
    /// Normalizes the name of an entry, and rejects a name which could lead outside of the target
    /// directory with an InvalidDataException: an empty, rooted, or drive-qualified name, or one which
    /// contains "." or "..".
    /// </summary>
    /// <param name="entryName">The name as the ZIP file holds it.</param>
    /// <returns>The name with forward slashes.</returns>
    public static string Normalize(string entryName)
    {
        var normalizedEntryName = entryName.Replace('\\', SEPARATOR);
        if (string.IsNullOrWhiteSpace(normalizedEntryName))
            throw new InvalidDataException("The archive contains an empty entry name.");

        if (normalizedEntryName.Contains('\0'))
            throw new InvalidDataException($"The archive contains an invalid entry name: '{entryName}'.");

        if (normalizedEntryName.StartsWith(SEPARATOR))
            throw new InvalidDataException($"The archive contains a rooted entry name: '{entryName}'.");

        if (normalizedEntryName is [_, ':', ..])
            throw new InvalidDataException($"The archive contains a drive-qualified entry name: '{entryName}'.");

        var pathSegments = normalizedEntryName.Split(SEPARATOR, StringSplitOptions.RemoveEmptyEntries);
        if (pathSegments.Length == 0 || pathSegments.Any(segment => segment is "." or ".."))
            throw new InvalidDataException($"The archive contains an unsafe entry name: '{entryName}'.");

        return normalizedEntryName;
    }

    /// <summary>
    /// Normalizes the name of an entry, cf. Normalize, without throwing.
    /// </summary>
    /// <param name="entryName">The name as the ZIP file holds it.</param>
    /// <param name="normalizedEntryName">The name with forward slashes, or an empty string when it is unsafe.</param>
    /// <returns>True when the name is safe.</returns>
    public static bool TryNormalize(string entryName, out string normalizedEntryName)
    {
        try
        {
            normalizedEntryName = Normalize(entryName);
            return true;
        }
        catch (InvalidDataException)
        {
            normalizedEntryName = string.Empty;
            return false;
        }
    }

    /// <summary>
    /// The path at which an entry is written below the target directory. A path which would lie
    /// outside of it is rejected with an InvalidDataException.
    /// </summary>
    /// <param name="destinationDirectory">The target directory.</param>
    /// <param name="normalizedEntryName">The entry name, as Normalize returned it.</param>
    /// <returns>The full path of the entry.</returns>
    public static string GetDestinationPath(string destinationDirectory, string normalizedEntryName)
    {
        var fullDestinationDirectory = Path.GetFullPath(destinationDirectory);
        if (!fullDestinationDirectory.EndsWith(Path.DirectorySeparatorChar))
            fullDestinationDirectory += Path.DirectorySeparatorChar;

        var pathSegments = normalizedEntryName.Split(SEPARATOR, StringSplitOptions.RemoveEmptyEntries);
        var destinationPath = Path.GetFullPath(Path.Combine(fullDestinationDirectory, Path.Combine(pathSegments)));
        if (!destinationPath.StartsWith(fullDestinationDirectory, StringComparison.Ordinal))
            throw new InvalidDataException($"The archive contains an entry outside the destination directory: '{normalizedEntryName}'.");

        return destinationPath;
    }
}