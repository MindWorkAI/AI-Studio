using System.IO.Compression;

namespace AIStudio.Tools.PluginSystem;

public static class PluginArchive
{
    // Compatibility shim for Windows-created ZIPs with backslashes in entry names (dotnet/runtime#27620).
    // See documentation/compatibility-shims/2026-07-enterprise-config-zip-backslashes.md.
    public static void Extract(string sourceArchiveFileName, string destinationDirectory)
    {
        using var archive = ZipFile.OpenRead(sourceArchiveFileName);
        Directory.CreateDirectory(destinationDirectory);

        var destinationDirectoryFullPath = Path.GetFullPath(destinationDirectory);
        if (!destinationDirectoryFullPath.EndsWith(Path.DirectorySeparatorChar))
            destinationDirectoryFullPath += Path.DirectorySeparatorChar;

        foreach (var entry in archive.Entries)
        {
            var normalizedEntryName = NormalizeEntryName(entry.FullName);
            var destinationPath = GetEntryDestinationPath(destinationDirectoryFullPath, normalizedEntryName);

            if (normalizedEntryName.EndsWith('/'))
            {
                if (entry.Length != 0)
                    throw new InvalidDataException($"The plugin archive contains a directory entry with data: '{entry.FullName}'.");

                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            entry.ExtractToFile(destinationPath);
        }
    }

    private static string NormalizeEntryName(string entryName)
    {
        var normalizedEntryName = entryName.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalizedEntryName))
            throw new InvalidDataException("The plugin archive contains an empty entry name.");

        if (normalizedEntryName.Contains('\0'))
            throw new InvalidDataException($"The plugin archive contains an invalid entry name: '{entryName}'.");

        if (normalizedEntryName.StartsWith('/'))
            throw new InvalidDataException($"The plugin archive contains a rooted entry name: '{entryName}'.");

        if (normalizedEntryName is [_, ':', ..])
            throw new InvalidDataException($"The plugin archive contains a drive-qualified entry name: '{entryName}'.");

        var pathSegments = normalizedEntryName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pathSegments.Length == 0 || pathSegments.Any(segment => segment is "." or ".."))
            throw new InvalidDataException($"The plugin archive contains an unsafe entry name: '{entryName}'.");

        return normalizedEntryName;
    }

    private static string GetEntryDestinationPath(string destinationDirectoryFullPath, string normalizedEntryName)
    {
        var pathSegments = normalizedEntryName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var relativePath = Path.Combine(pathSegments);
        var destinationPath = Path.GetFullPath(Path.Combine(destinationDirectoryFullPath, relativePath));
        if (!destinationPath.StartsWith(destinationDirectoryFullPath, StringComparison.Ordinal))
            throw new InvalidDataException($"The plugin archive contains an entry outside the destination directory: '{normalizedEntryName}'.");

        return destinationPath;
    }
}
