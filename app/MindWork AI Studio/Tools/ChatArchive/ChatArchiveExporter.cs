using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;

using AIStudio.Tools.Metadata;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.ChatArchive;

/// <summary>
/// Writes selected workspaces and temporary chats into a chat archive.
/// </summary>
public static class ChatArchiveExporter
{
    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger(nameof(ChatArchiveExporter));

    /// <summary>
    /// File types which are compressed already. Deflating them again costs time without
    /// making the archive smaller.
    /// </summary>
    private static readonly HashSet<string> ALREADY_COMPRESSED_EXTENSIONS = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic", ".tiff",
        ".mp3", ".m4a", ".m4b", ".aac", ".ogg", ".opus", ".flac", ".wma",
        ".mp4", ".m4v", ".mov", ".mkv", ".webm", ".avi", ".wmv", ".flv",
        ".zip", ".7z", ".gz", ".bz2", ".xz", ".rar",
        ".pdf", ".docx", ".xlsx", ".pptx", ".odt", ".odp",
    };

    private static readonly int BUFFER_SIZE = 64 * 1024;

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ChatArchiveExporter).Namespace, nameof(ChatArchiveExporter));

    /// <summary>
    /// Exports the given workspaces and, when requested, all temporary chats into one archive.
    /// </summary>
    /// <param name="workspaceIds">The workspaces to export.</param>
    /// <param name="includeTemporaryChats">Whether to include all temporary chats.</param>
    /// <param name="includeChatFiles">Whether to include the app-owned files of the chats, such as transcripts.</param>
    /// <param name="archivePath">The target path of the archive. An existing file is replaced once the new archive is complete.</param>
    /// <param name="progress">Receives the export progress.</param>
    /// <param name="token">Cancels the export. A cancelled export leaves no archive behind and an existing one untouched.</param>
    /// <returns>The export result.</returns>
    public static async Task<ChatArchiveExportResult> ExportAsync(IReadOnlyList<Guid> workspaceIds, bool includeTemporaryChats, bool includeChatFiles, string archivePath, IProgress<ChatArchiveProgress>? progress, CancellationToken token)
    {
        //
        // The archive is written next to its target and only takes its place once complete.
        // Writing over the target directly would destroy the backup it replaces the moment the
        // export starts, and a failed or cancelled export would then leave none at all:
        //
        var temporaryArchivePath = $"{archivePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            //
            // Collect everything to export first, so that we know the total amount of
            // work before we start writing the archive:
            //
            var tree = await WorkspaceBehaviour.GetOrLoadWorkspaceTreeShellAsync();
            var selectedWorkspaces = new List<(Guid WorkspaceId, string Name, IReadOnlyList<WorkspaceTreeChat> Chats)>();
            foreach (var workspaceId in workspaceIds)
            {
                token.ThrowIfCancellationRequested();

                var workspace = tree.Workspaces.FirstOrDefault(candidate => candidate.WorkspaceId == workspaceId);
                if (workspace.WorkspaceId == Guid.Empty)
                {
                    LOG.LogWarning("Skipping workspace '{WorkspaceId}' for the export, because it is unknown.", workspaceId);
                    continue;
                }

                var chats = await WorkspaceBehaviour.GetWorkspaceChatsAsync(workspaceId);
                selectedWorkspaces.Add((workspaceId, workspace.Name, chats));
            }

            IReadOnlyList<WorkspaceTreeChat> temporaryChats = includeTemporaryChats ? tree.TemporaryChats : [];
            var totalChats = selectedWorkspaces.Sum(workspace => workspace.Chats.Count) + temporaryChats.Count;
            var processedChats = 0;
            var unreadableChats = 0;

            var manifestWorkspaces = new List<ChatArchiveManifestWorkspace>();
            List<ChatArchiveManifestChat> manifestTemporaryChats;

            await using (var archiveStream = new FileStream(temporaryArchivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BUFFER_SIZE, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create);

                // Workspace chats and temporary chats differ only in where they are stored:
                async Task<List<ChatArchiveManifestChat>> ExportChatsAsync(string entryPrefix, Guid workspaceId, IReadOnlyList<WorkspaceTreeChat> chats)
                {
                    var manifestChats = new List<ChatArchiveManifestChat>();
                    foreach (var chat in chats)
                    {
                        token.ThrowIfCancellationRequested();
                        progress?.Report(new(processedChats, totalChats));
                        processedChats++;

                        var manifestChat = await ExportChatAsync(archive, entryPrefix, workspaceId, chat, includeChatFiles, token);
                        if (manifestChat is not null)
                            manifestChats.Add(manifestChat);
                        else
                            unreadableChats++;
                    }

                    return manifestChats;
                }

                foreach (var workspace in selectedWorkspaces)
                {
                    token.ThrowIfCancellationRequested();

                    var workspaceEntryPrefix = $"{ChatArchiveFormat.WORKSPACES_DIRECTORY}{ChatArchiveFormat.ENTRY_SEPARATOR}{workspace.WorkspaceId}{ChatArchiveFormat.ENTRY_SEPARATOR}";
                    await WriteTextEntryAsync(archive, $"{workspaceEntryPrefix}{ChatArchiveFormat.NAME_FILE_NAME}", workspace.Name, token);

                    manifestWorkspaces.Add(new()
                    {
                        WorkspaceId = workspace.WorkspaceId,
                        Name = workspace.Name,
                        Chats = await ExportChatsAsync(workspaceEntryPrefix, workspace.WorkspaceId, workspace.Chats),
                    });
                }

                var temporaryChatsEntryPrefix = $"{ChatArchiveFormat.TEMPORARY_CHATS_DIRECTORY}{ChatArchiveFormat.ENTRY_SEPARATOR}";
                manifestTemporaryChats = await ExportChatsAsync(temporaryChatsEntryPrefix, Guid.Empty, temporaryChats);

                var manifest = new ChatArchiveManifest
                {
                    FormatVersion = ChatArchiveFormat.FORMAT_VERSION,
                    ExportedAt = DateTimeOffset.UtcNow,
                    AppVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<MetaDataAttribute>()?.Version ?? "unknown",
                    Workspaces = manifestWorkspaces,
                    TemporaryChats = manifestTemporaryChats,
                };

                var manifestJson = JsonSerializer.Serialize(manifest, WorkspaceBehaviour.JSON_OPTIONS);
                await WriteTextEntryAsync(archive, ChatArchiveFormat.MANIFEST_FILE_NAME, manifestJson, token);
            }

            token.ThrowIfCancellationRequested();
            File.Move(temporaryArchivePath, archivePath, overwrite: true);

            progress?.Report(new(processedChats, totalChats));

            var exportedChats = manifestWorkspaces.SelectMany(workspace => workspace.Chats).Concat(manifestTemporaryChats).ToList();
            LOG.LogInformation("Exported {ChatCount} chats from {WorkspaceCount} workspaces to the archive '{ArchivePath}'.", exportedChats.Count, manifestWorkspaces.Count, archivePath);

            return new(
                true,
                archivePath,
                manifestWorkspaces.Count,
                exportedChats.Count,
                exportedChats.Sum(chat => chat.IncludedFiles),
                unreadableChats,
                string.Empty,
                false);
        }
        catch (OperationCanceledException)
        {
            TryDeleteArchive(temporaryArchivePath);
            LOG.LogInformation("The user cancelled the export to '{ArchivePath}'. The incomplete archive was removed.", archivePath);
            return new(true, archivePath, 0, 0, 0, 0, string.Empty, true);
        }
        catch (Exception exception)
        {
            TryDeleteArchive(temporaryArchivePath);
            LOG.LogError(exception, "Failed to export chats to the archive '{ArchivePath}'.", archivePath);
            return new(false, archivePath, 0, 0, 0, 0, string.Format(TB("Unexpected error: {0}"), exception.Message), false);
        }
    }

    /// <summary>
    /// Determines whether the given selection contains any app-owned file which the export
    /// could include, so that the user is only asked about them when there are any.
    /// </summary>
    /// <param name="workspaceIds">The workspaces of the selection.</param>
    /// <param name="includeTemporaryChats">Whether the selection includes all temporary chats.</param>
    /// <param name="token">Cancels the search.</param>
    /// <returns>Whether at least one chat of the selection owns a file.</returns>
    public static async Task<bool> HasChatFilesAsync(IReadOnlyList<Guid> workspaceIds, bool includeTemporaryChats, CancellationToken token)
    {
        var tree = await WorkspaceBehaviour.GetOrLoadWorkspaceTreeShellAsync();
        foreach (var workspaceId in workspaceIds)
        {
            token.ThrowIfCancellationRequested();

            var chats = await WorkspaceBehaviour.GetWorkspaceChatsAsync(workspaceId);
            if (ChatArchiveChatFiles.HasAny(chats.Select(chat => chat.ChatPath), token))
                return true;
        }

        return includeTemporaryChats && ChatArchiveChatFiles.HasAny(tree.TemporaryChats.Select(chat => chat.ChatPath), token);
    }

    /// <summary>
    /// Writes one chat into the archive: its name, optionally the files AI Studio owns for it,
    /// and its chat thread.
    /// </summary>
    /// <remarks>
    /// Documents the user attached are never written into the archive. Their paths stay
    /// absolute in the chat thread, which keeps them working wherever the documents live,
    /// for example on a document server, and keeps the archive free of copies of them.
    /// </remarks>
    /// <returns>The manifest entry for the chat, or null when the chat could not be read.</returns>
    private static async Task<ChatArchiveManifestChat?> ExportChatAsync(ZipArchive archive, string entryPrefix, Guid workspaceId, WorkspaceTreeChat chat, bool includeChatFiles, CancellationToken token)
    {
        var thread = await WorkspaceBehaviour.LoadChatAsync(new(workspaceId, chat.ChatId));
        if (thread is null)
        {
            LOG.LogWarning("Skipping chat '{ChatId}' of workspace '{WorkspaceId}' for the export, because it could not be read.", chat.ChatId, workspaceId);
            return null;
        }

        var chatDirectory = chat.ChatPath;
        var chatEntryPrefix = $"{entryPrefix}{chat.ChatId}{ChatArchiveFormat.ENTRY_SEPARATOR}";
        await WriteTextEntryAsync(archive, $"{chatEntryPrefix}{ChatArchiveFormat.NAME_FILE_NAME}", chat.Name, token);

        //
        // Write the files before rewriting any path: only a file which is really in the
        // archive may become an archive-relative path. Otherwise a file which could not be
        // read would leave a relative path behind, which the import cannot resolve.
        //
        var includedFiles = new Dictionary<string, string>(PathTools.COMPARER);
        if (includeChatFiles)
        {
            foreach (var (chatRelativePath, filePath) in ChatArchiveChatFiles.Collect(chatDirectory))
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    await WriteFileEntryAsync(archive, $"{chatEntryPrefix}{chatRelativePath}", filePath, token);
                    includedFiles[chatRelativePath] = filePath;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    LOG.LogWarning(exception, "Failed to add the file '{FilePath}' of chat '{ChatId}' to the archive.", filePath, chat.ChatId);
                }
            }

            ChatArchiveAttachmentPaths.Rewrite(thread, originalPath =>
                TryGetChatRelativePath(chatDirectory, originalPath, out var relativePath) && includedFiles.ContainsKey(relativePath)
                    ? relativePath
                    : originalPath);
        }

        var threadJson = JsonSerializer.Serialize(thread, WorkspaceBehaviour.JSON_OPTIONS);
        await WriteTextEntryAsync(archive, $"{chatEntryPrefix}{ChatArchiveFormat.THREAD_FILE_NAME}", threadJson, token);

        return new()
        {
            ChatId = chat.ChatId,
            Name = chat.Name,
            LastEditTime = chat.LastEditTime,
            IncludedFiles = includedFiles.Count,
        };
    }

    /// <summary>
    /// Determines whether the given path points into the chat directory and returns the
    /// archive-relative path for it.
    /// </summary>
    private static bool TryGetChatRelativePath(string chatDirectory, string path, out string relativePath)
    {
        relativePath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            var relative = Path.GetRelativePath(Path.GetFullPath(chatDirectory), Path.GetFullPath(path));
            if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", PathTools.COMPARISON))
                return false;

            relativePath = relative.Replace(Path.DirectorySeparatorChar, ChatArchiveFormat.ENTRY_SEPARATOR);
            return true;
        }
        catch (Exception exception)
        {
            LOG.LogWarning(exception, "Could not determine whether the path '{Path}' belongs to the chat directory '{ChatDirectory}'.", path, chatDirectory);
            return false;
        }
    }

    private static async Task WriteTextEntryAsync(ZipArchive archive, string entryName, string content, CancellationToken token)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var entryStream = entry.Open();
        await using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));
        await writer.WriteAsync(content.AsMemory(), token);
    }

    private static async Task WriteFileEntryAsync(ZipArchive archive, string entryName, string filePath, CancellationToken token)
    {
        // Compressing an already compressed attachment costs time for nothing:
        var compressionLevel = ALREADY_COMPRESSED_EXTENSIONS.Contains(Path.GetExtension(filePath))
            ? CompressionLevel.Fastest
            : CompressionLevel.Optimal;

        var entry = archive.CreateEntry(entryName, compressionLevel);

        // The ZIP format cannot store timestamps before 1980:
        var lastWriteTime = File.GetLastWriteTime(filePath);
        if (lastWriteTime.Year >= 1980)
            entry.LastWriteTime = lastWriteTime;

        await using var entryStream = entry.Open();
        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BUFFER_SIZE, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await fileStream.CopyToAsync(entryStream, token);
    }

    private static void TryDeleteArchive(string archivePath)
    {
        if (!File.Exists(archivePath))
            return;

        try
        {
            File.Delete(archivePath);
        }
        catch (Exception exception)
        {
            LOG.LogWarning(exception, "Failed to delete the incomplete archive '{ArchivePath}'.", archivePath);
        }
    }
}