using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.Services.Indexing;

internal sealed partial class FileSourceIndexer
{
    /// <summary>
    /// Reconnects unchanged files before enumeration and missing-file cleanup. In particular, media
    /// must already be known under their new paths when enumeration asks whether they need transcription.
    /// </summary>
    private async Task<bool> RelocateStoredFilesAsync(IndexedRunContext context, IDataSource dataSource, CancellationToken token)
    {
        DocumentRunProgress? progress = null;
        foreach (var (oldPath, record) in context.Manifest.Files.ToArray())
        {
            token.ThrowIfCancellationRequested();
            var file = FindRelocatedFile(dataSource, oldPath, record);
            if (file is null || (!string.Equals(oldPath, file.FullName, StringComparison.OrdinalIgnoreCase) && context.Manifest.Files.ContainsKey(file.FullName)))
                continue;

            progress ??= DocumentRunProgress.FromStoredState(context, context.Manifest.Files.Count, logger);
            if (progress.HasDoneItsShare(DateTimeOffset.UtcNow))
                return false;
            progress.Publish();

            var relativePath = this.TryGetRelativePath(dataSource, file);
            // Use the metadata which passed the reuse check. If the file changes during these
            // writes, enumeration must see a different fingerprint and index its new content.
            var fingerprint = BuildMetadataHash("file", file.FullName, file.Name, record.LastWriteUtc.UtcDateTime.Ticks.ToString(), record.FileSize.ToString());

            // Vectors first, then SQLite. Selecting by the unchanged parent id makes this safe to
            // retry if cancellation or a crash falls between the two writes. A failure aborts the
            // run before missing-file cleanup can remove the documents we are reconnecting.
            await context.VectorStore.RelocateEmbeddingFileAsync(context.CollectionName, record.ParentFileId, file.FullName, relativePath, fingerprint, token);
            token.ThrowIfCancellationRequested();
            await context.IndexStore.RelocateFileAsync(dataSource.Id, record.ParentFileId, file.FullName, relativePath, fingerprint, token);
            context.Manifest.Files.Remove(oldPath);
            context.Manifest.Files[file.FullName] = record with { Fingerprint = fingerprint, RelativePath = relativePath };

            logger.LogInformation("Reused stored embeddings for relocated file '{OldPath}' at '{NewPath}' in data source '{DataSourceId}'.", oldPath, file.FullName, dataSource.Id);
        }

        return true;
    }

    /// <summary>
    /// Uses the same size and modification-time check as ordinary indexing, matching a folder's
    /// documents by relative path. It never searches another folder for a missing document.
    /// </summary>
    internal static FileInfo? FindRelocatedFile(IDataSource dataSource, string oldPath, EmbeddedFileRecord record)
    {
        if (string.IsNullOrEmpty(record.ParentFileId))
            return null;

        string targetPath;
        switch (dataSource)
        {
            case DataSourceLocalFile localFile:
                targetPath = Path.GetFullPath(localFile.FilePath);
                break;

            case DataSourceLocalDirectory localDirectory when !string.IsNullOrWhiteSpace(record.RelativePath) && !Path.IsPathRooted(record.RelativePath):
                var root = Path.GetFullPath(localDirectory.Path);
                targetPath = Path.GetFullPath(Path.Combine(root, record.RelativePath));
                if (!targetPath.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    return null;
                break;

            default:
                return null;
        }

        if (string.Equals(oldPath, targetPath, StringComparison.Ordinal)
            || !string.Equals(Path.GetExtension(oldPath), Path.GetExtension(targetPath), StringComparison.OrdinalIgnoreCase))
            return null;

        var file = new FileInfo(targetPath);
        return file.Exists && file.Length == record.FileSize && file.LastWriteTimeUtc == record.LastWriteUtc.UtcDateTime ? file : null;
    }
}
