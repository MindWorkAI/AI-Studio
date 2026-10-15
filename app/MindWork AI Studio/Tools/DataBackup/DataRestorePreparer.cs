using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

using AIStudio.Tools.PluginSystem;

using SettingsVersion = AIStudio.Settings.Version;

namespace AIStudio.Tools.DataBackup;

/// <summary>
/// Prepares a restore: unpacks a backup next to the data and configuration folders, so that the next
/// start can put it in place, cf. DataRestoreFormat.
/// </summary>
/// <remarks>
/// Nothing in use changes here. While AI Studio runs, its files are open, and on Windows, they cannot be
/// replaced at all. The runtime replaces them at the next start, before the app opens any of them, and
/// rolls back when that fails.
/// </remarks>
public static class DataRestorePreparer
{
    private const int BUFFER_SIZE = 64 * 1024;

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(DataRestorePreparer).Namespace, nameof(DataRestorePreparer));

    /// <summary>
    /// A file of the backup to unpack, and where it goes.
    /// </summary>
    private sealed record PendingFile(ZipArchiveEntry Entry, DataBackupManifestEntry Listed, string DestinationPath);

    /// <summary>
    /// Unpacks a backup for the next start.
    /// </summary>
    /// <remarks>
    /// Every file has to match the size and the hash the manifest lists for it: a restore replaces all
    /// data, so a damaged backup must not get that far. A file the manifest does not list, or one
    /// which belongs to no element the restore replaces, stays in the backup. A restore prepared
    /// before is discarded first; one which fails or is cancelled leaves nothing behind.
    /// </remarks>
    /// <param name="archivePath">The backup.</param>
    /// <param name="target">Where the restore goes.</param>
    /// <param name="progress">Receives how many files are unpacked.</param>
    /// <param name="token">Cancels the preparation.</param>
    /// <returns>Whether the restore is prepared, or why not.</returns>
    public static async Task<DataRestoreResult> PrepareAsync(string archivePath, DataRestoreTarget target, IProgress<DataRestoreProgress>? progress, CancellationToken token)
    {
        Discard(target);
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var manifestResult = await DataBackupReader.ReadManifestAsync(archive, token);
            if (manifestResult.Manifest is not { } manifest)
                return new(false, false, manifestResult.Issue);

            var versionIssue = CheckVersions(manifest, target);
            if (!string.IsNullOrWhiteSpace(versionIssue))
                return new(false, false, versionIssue);

            var elements = DetermineElements(manifest, target);
            var files = DetermineFiles(archive, manifest, elements, target);
            progress?.Report(new(0, files.Count));

            var buffer = new byte[BUFFER_SIZE];
            for (var index = 0; index < files.Count; index++)
            {
                token.ThrowIfCancellationRequested();
                if (!await UnpackAsync(files[index], buffer, token))
                {
                    Discard(target);
                    return new(false, false, string.Format(TB("The backup is damaged: the file '{0}' does not match its content list."), files[index].Listed.Name));
                }

                progress?.Report(new(index + 1, files.Count));
            }

            var pendingDirectory = PendingDirectory(target.DataDirectory);
            Directory.CreateDirectory(pendingDirectory);

            var plan = new DataRestorePlan(DataRestoreFormat.PLAN_FORMAT_VERSION, DateTimeOffset.UtcNow, manifest.CreatedAtUtc, manifest.AppVersion, elements);
            await File.WriteAllBytesAsync(Path.Join(pendingDirectory, DataRestoreFormat.PLAN_FILE_NAME), JsonSerializer.SerializeToUtf8Bytes(plan, DataBackupFormat.JSON_OPTIONS), token);

            // Last of all: from now on, the next start applies the restore.
            await File.WriteAllTextAsync(Path.Join(pendingDirectory, DataRestoreFormat.READY_FILE_NAME), plan.PreparedAtUtc.ToString("O"), token);
            return new(true, false, string.Empty);
        }
        catch (InvalidDataException)
        {
            Discard(target);
            return new(false, false, TB("This file is no backup of AI Studio, or it was not written completely."));
        }
        catch
        {
            Discard(target);
            throw;
        }
    }

    /// <summary>
    /// Removes a prepared restore, complete or not, so that the next start leaves everything as it is.
    /// </summary>
    /// <remarks>
    /// A restore which the runtime has not finished stays: its journal is the only way back to the data
    /// it replaced, cf. DataRestoreFollowUp.IsRestoreUnfinished.
    /// </remarks>
    /// <param name="target">Where the restore was to go.</param>
    public static void Discard(DataRestoreTarget target)
    {
        if (DataRestoreFollowUp.IsRestoreUnfinished(target.DataDirectory))
            throw new InvalidOperationException("The runtime has not finished the last restore yet, so it cannot be discarded.");

        foreach (var pendingDirectory in new[] { PendingDirectory(target.DataDirectory), PendingDirectory(target.ConfigDirectory) })
            if (Directory.Exists(pendingDirectory))
                Directory.Delete(pendingDirectory, true);
    }

    /// <summary>
    /// Whether a restore is prepared completely and waits for the next start.
    /// </summary>
    /// <param name="target">Where the restore goes.</param>
    /// <returns>True when the next start applies a restore.</returns>
    public static bool IsPrepared(DataRestoreTarget target) => DataRestoreFollowUp.IsRestorePending(target.DataDirectory);

    private static string PendingDirectory(string directory) => DataRestoreFormat.DirectoryIn(directory, DataRestoreFormat.PENDING_DIRECTORY);

    /// <summary>
    /// Refuses a backup of a later version of AI Studio. Its settings or its index may have a form
    /// this version cannot read, and it would refuse to write them, or break them.
    /// </summary>
    /// <returns>Why the backup cannot be restored, or an empty string.</returns>
    private static string CheckVersions(DataBackupManifest manifest, DataRestoreTarget target)
    {
        var settingsAreNewer = !Enum.TryParse<SettingsVersion>(manifest.SettingsVersion, out var settingsVersion) || settingsVersion > target.SettingsVersion;
        var appIsNewer = Version.TryParse(manifest.AppVersion, out var backupAppVersion) && Version.TryParse(target.AppVersion, out var appVersion) && backupAppVersion > appVersion;
        return settingsAreNewer || appIsNewer
            ? TB("This backup was made by a newer version of AI Studio. Please update AI Studio first.")
            : string.Empty;
    }

    /// <summary>
    /// Determines the elements the restore replaces: those of the list of the backup contents which the
    /// backup holds anything for.
    /// </summary>
    /// <remarks>
    /// An element the backup holds nothing for stays as it is, since nothing tells whether it was empty
    /// or whether the version of AI Studio which made the backup knew it at all. The indexes are
    /// replaced only when the backup holds both of them, cf. DataBackupManifest.IncludesDataSourceIndexes.
    /// </remarks>
    private static List<DataRestoreElement> DetermineElements(DataBackupManifest manifest, DataRestoreTarget target)
    {
        var elements = new List<DataRestoreElement>();
        foreach (var entry in DataBackupContents.ENTRIES.Where(entry => entry.IsIncluded))
        {
            if (entry.Kind is DataBackupEntryKind.DATA_SOURCE_INDEX or DataBackupEntryKind.VECTOR_STORES && !manifest.IncludesDataSourceIndexes)
                continue;

            var entryName = DataBackupFormat.EntryName(entry.Location, entry.RelativePath);
            if (!manifest.Entries.Any(listed => DataBackupContents.Covers(entryName, listed.Name)))
                continue;

            var keep = DataBackupContents.ENTRIES
                .Where(excluded => !excluded.IsIncluded && excluded.Location == entry.Location && entry.IsFolder && DataBackupContents.Covers(entry.RelativePath, excluded.RelativePath))
                .Select(excluded => excluded.RelativePath)
                .Concat(entry.Kind is DataBackupEntryKind.PLUGINS ? target.OrganizationPluginFolders : [])
                .ToList();

            elements.Add(new(entry.Location, entry.RelativePath, keep));
        }

        return elements;
    }

    /// <summary>
    /// Determines the files to unpack: those the manifest lists, which belong to an element the restore
    /// replaces, and which the element does not keep.
    /// </summary>
    private static List<PendingFile> DetermineFiles(ZipArchive archive, DataBackupManifest manifest, IReadOnlyList<DataRestoreElement> elements, DataRestoreTarget target)
    {
        var listedEntries = manifest.Entries.ToDictionary(listed => listed.Name, StringComparer.Ordinal);
        var files = new List<PendingFile>();
        foreach (var entry in archive.Entries)
        {
            var entryName = ZipEntryNames.Normalize(entry.FullName);
            if (!listedEntries.TryGetValue(entryName, out var listed))
                continue;

            foreach (var element in elements)
            {
                var root = DataBackupFormat.EntryName(element.Location, string.Empty);
                if (!entryName.StartsWith(root, StringComparison.Ordinal))
                    continue;

                var relativePath = entryName[root.Length..];
                if (!DataBackupContents.Covers(element.RelativePath, relativePath) || element.Keep.Any(kept => DataBackupContents.Covers(kept, relativePath)))
                    continue;

                var directory = element.Location is DataBackupLocation.CONFIG_DIRECTORY ? target.ConfigDirectory : target.DataDirectory;
                files.Add(new(entry, listed, ZipEntryNames.GetDestinationPath(PendingDirectory(directory), relativePath)));
                break;
            }
        }

        return files;
    }

    /// <summary>
    /// Unpacks one file, and checks its size and hash while it streams through.
    /// </summary>
    /// <returns>False when the file does not match the manifest.</returns>
    private static async Task<bool> UnpackAsync(PendingFile file, byte[] buffer, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file.DestinationPath)!);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long size = 0;
        await using (var source = file.Entry.Open())
        await using (var destination = new FileStream(file.DestinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BUFFER_SIZE, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            int read;
            while ((read = await source.ReadAsync(buffer, token)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                await destination.WriteAsync(buffer.AsMemory(0, read), token);
                size += read;
            }
        }

        if (size != file.Listed.Size || Convert.ToHexStringLower(hash.GetHashAndReset()) != file.Listed.Sha256)
            return false;

        // The time of the last change tells, e.g., the order of the chats in a workspace:
        File.SetLastWriteTime(file.DestinationPath, file.Entry.LastWriteTime.LocalDateTime);
        return true;
    }
}