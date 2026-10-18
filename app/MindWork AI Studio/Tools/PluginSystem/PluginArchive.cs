using System.IO.Compression;

namespace AIStudio.Tools.PluginSystem;

public static class PluginArchive
{
    /// <summary>
    /// The file extension of plugin archives.
    /// </summary>
    /// <remarks>
    /// Keep in sync with SHARE_FILE_EXTENSION in runtime/src/share_sheet.rs: the runtime only hands
    /// archives with this extension to the native share sheet.
    /// </remarks>
    public const string PLUGIN_FILE_EXTENSION = ".mwplugin";


    // Compatibility shim for Windows-created ZIPs with backslashes in entry names (dotnet/runtime#27620);
    // remove after dotnet/runtime#27620 and #41914 are fixed.
    // See documentation/compatibility-shims/2026-07-plugin-archive-zip-backslashes.md.
    public static void Extract(string sourceArchiveFileName, string destinationDirectory)
    {
        using var archive = ZipFile.OpenRead(sourceArchiveFileName);
        Directory.CreateDirectory(destinationDirectory);

        foreach (var entry in archive.Entries)
        {
            var normalizedEntryName = ZipEntryNames.Normalize(entry.FullName);
            var destinationPath = ZipEntryNames.GetDestinationPath(destinationDirectory, normalizedEntryName);

            if (normalizedEntryName.EndsWith(ZipEntryNames.SEPARATOR))
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
}