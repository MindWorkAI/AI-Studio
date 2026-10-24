using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIStudio.Tools.DataBackup;

/// <summary>
/// Writes a backup, cf. DataBackupFormat.
/// </summary>
/// <remarks>
/// Every file is streamed into the backup, so a backup of many gigabytes needs no more memory than
/// a small one. The caller writes the backup straight into the file the user chose: in the Flatpak,
/// the portal grants access to exactly that file, and to nothing next to it.
/// </remarks>
public static class DataBackupWriter
{
    private const int BUFFER_SIZE = 64 * 1024;

    /// <summary>
    /// The earliest time a ZIP file can store.
    /// </summary>
    private static readonly DateTimeOffset EARLIEST_ZIP_TIME = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A file to take along, and where it goes in the backup.
    /// </summary>
    private sealed record BackupFile(string EntryName, string SourcePath);

    /// <summary>
    /// Writes a backup into the given stream.
    /// </summary>
    /// <remarks>
    /// A file which is gone by the time it is written, e.g. a temporary chat AI Studio deleted
    /// meanwhile, is left out. A file which cannot be read fails the backup, though: a backup which
    /// quietly lacks something is worse than none.
    /// </remarks>
    /// <param name="target">Where to write the backup. It stays open.</param>
    /// <param name="sources">Where the files come from.</param>
    /// <param name="manifest">What to note in the manifest. The entries are filled in here.</param>
    /// <param name="progress">Receives how many files are written.</param>
    /// <param name="token">Cancels the backup.</param>
    /// <returns>The manifest as the backup holds it.</returns>
    public static async Task<DataBackupManifest> WriteAsync(Stream target, DataBackupSources sources, DataBackupManifest manifest, IProgress<DataBackupProgress>? progress, CancellationToken token)
    {
        var files = CollectFiles(sources, token);
        var totalFiles = files.Count + 1;
        var entries = new List<DataBackupManifestEntry>(totalFiles);
        var buffer = new byte[BUFFER_SIZE];

        progress?.Report(new(DataBackupPhase.WRITING, 0, totalFiles));
        using (var archive = new ZipArchive(target, ZipArchiveMode.Create, leaveOpen: true))
        {
            var settingsEntry = DataBackupContents.ENTRIES.First(entry => entry.Kind is DataBackupEntryKind.SETTINGS);
            await using (var settingsStream = new MemoryStream(Encoding.UTF8.GetBytes(sources.SettingsJson), writable: false))
                entries.Add(await WriteEntryAsync(archive, DataBackupFormat.EntryName(settingsEntry.Location, settingsEntry.RelativePath), settingsStream, DateTimeOffset.Now, buffer, token));

            progress?.Report(new(DataBackupPhase.WRITING, entries.Count, totalFiles));
            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();

                FileStream source;
                try
                {
                    source = new FileStream(file.SourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, BUFFER_SIZE, FileOptions.Asynchronous | FileOptions.SequentialScan);
                }
                catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
                {
                    continue;
                }

                await using (source)
                    entries.Add(await WriteEntryAsync(archive, file.EntryName, source, File.GetLastWriteTime(file.SourcePath), buffer, token));

                progress?.Report(new(DataBackupPhase.WRITING, entries.Count, totalFiles));
            }

            manifest = manifest with { Entries = entries };
            await using (var manifestStream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(manifest, DataBackupFormat.JSON_OPTIONS), writable: false))
                await WriteEntryAsync(archive, DataBackupFormat.MANIFEST_ENTRY_NAME, manifestStream, DateTimeOffset.Now, buffer, token);
        }

        return manifest;
    }

    /// <summary>
    /// Collects the files of the data folder which belong into a backup, cf. DataBackupContents.
    /// </summary>
    /// <remarks>
    /// The settings are no part of it: they come from memory. The indexes and the provenance store
    /// come from their copies.
    /// </remarks>
    private static List<BackupFile> CollectFiles(DataBackupSources sources, CancellationToken token)
    {
        var excludedPaths = DataBackupContents.ENTRIES
            .Where(entry => !entry.IsIncluded && entry.Location is DataBackupLocation.DATA_DIRECTORY)
            .Select(entry => entry.RelativePath)
            .Concat(sources.OrganizationPluginFolders)
            .ToList();

        var files = new List<BackupFile>();
        foreach (var entry in DataBackupContents.ENTRIES.Where(entry => entry.IsIncluded && entry.Location is DataBackupLocation.DATA_DIRECTORY))
        {
            var root = entry switch
            {
                { ComesFromIndexCopies: true } => sources.IndexCopiesDirectory,
                { ComesFromProvenanceCopy: true } when !string.IsNullOrWhiteSpace(sources.ProvenanceCopyDirectory) => sources.ProvenanceCopyDirectory,
                _ => sources.DataDirectory,
            };

            if (string.IsNullOrWhiteSpace(root))
                continue;

            var path = Path.Join(root, entry.RelativePath.Replace(DataBackupContents.SEPARATOR, Path.DirectorySeparatorChar));
            if (!entry.IsFolder)
            {
                if (File.Exists(path))
                    files.Add(new(DataBackupFormat.EntryName(entry.Location, entry.RelativePath), path));

                continue;
            }

            if (!Directory.Exists(path))
                continue;

            //
            // Hidden files count as well, since on Linux and macOS a dot makes a file hidden. Links
            // are not followed, so nothing outside the folder gets into the backup. A folder which
            // cannot be read fails the backup instead of leaving a gap:
            //
            var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false };
            foreach (var filePath in Directory.EnumerateFiles(path, "*", options))
            {
                token.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(root, filePath).Replace(Path.DirectorySeparatorChar, DataBackupContents.SEPARATOR);
                if (excludedPaths.Any(excludedPath => DataBackupContents.Covers(excludedPath, relativePath)))
                    continue;

                files.Add(new(DataBackupFormat.EntryName(DataBackupLocation.DATA_DIRECTORY, relativePath), filePath));
            }
        }

        return files;
    }

    /// <summary>
    /// Writes one entry, and notes its size and hash while it streams through.
    /// </summary>
    private static async Task<DataBackupManifestEntry> WriteEntryAsync(ZipArchive archive, string entryName, Stream source, DateTimeOffset lastWriteTime, byte[] buffer, CancellationToken token)
    {
        var compressionLevel = DataBackupFormat.ALREADY_COMPRESSED_EXTENSIONS.Contains(Path.GetExtension(entryName))
            ? CompressionLevel.NoCompression
            : CompressionLevel.Optimal;

        var entry = archive.CreateEntry(entryName, compressionLevel);

        // The time of the last change tells, e.g., the order of the chats in a workspace:
        entry.LastWriteTime = lastWriteTime < EARLIEST_ZIP_TIME ? DateTimeOffset.Now : lastWriteTime;

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long size = 0;
        await using (var entryStream = entry.Open())
        {
            int read;
            while ((read = await source.ReadAsync(buffer, token)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                await entryStream.WriteAsync(buffer.AsMemory(0, read), token);
                size += read;
            }
        }

        return new(entryName, size, Convert.ToHexStringLower(hash.GetHashAndReset()));
    }
}