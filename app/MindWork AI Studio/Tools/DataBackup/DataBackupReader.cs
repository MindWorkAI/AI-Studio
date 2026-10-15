using System.IO.Compression;
using System.Text.Json;

using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.DataBackup;

/// <summary>
/// Reads what a backup holds, cf. DataBackupFormat.
/// </summary>
/// <remarks>
/// Open a backup with ZipArchiveMode.Read and read its entries as streams. ZipArchiveMode.Update would
/// buffer an entry in memory, which ends at 2 GB, and a backup may hold larger files.
/// </remarks>
public static class DataBackupReader
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(DataBackupReader).Namespace, nameof(DataBackupReader));

    /// <summary>
    /// Reads the manifest of a backup, and checks that this version of AI Studio can use the backup.
    /// </summary>
    /// <param name="archive">The backup, opened for reading.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The manifest, or why the backup cannot be used.</returns>
    public static async Task<DataBackupManifestReadResult> ReadManifestAsync(ZipArchive archive, CancellationToken token)
    {
        var manifestEntry = archive.GetEntry(DataBackupFormat.MANIFEST_ENTRY_NAME);
        if (manifestEntry is null)
            return new(null, TB("This file is no backup of AI Studio, or it was not written completely."));

        DataBackupManifest? manifest;
        try
        {
            await using var manifestStream = manifestEntry.Open();
            manifest = await JsonSerializer.DeserializeAsync<DataBackupManifest>(manifestStream, DataBackupFormat.JSON_OPTIONS, token);
        }
        catch (JsonException)
        {
            manifest = null;
        }

        if (manifest is null)
            return new(null, TB("The content list of this backup could not be read."));

        if (manifest.FormatVersion > DataBackupFormat.FORMAT_VERSION)
            return new(null, TB("This backup was made by a newer version of AI Studio. Please update AI Studio first."));

        return new(manifest, string.Empty);
    }
}