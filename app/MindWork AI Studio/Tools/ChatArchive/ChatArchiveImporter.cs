using System.IO.Compression;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.ChatArchive;

/// <summary>
/// Reads chat archives and writes their contents back into the chat storage.
/// </summary>
public static class ChatArchiveImporter
{
    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger(nameof(ChatArchiveImporter));

    private static readonly int BUFFER_SIZE = 64 * 1024;

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ChatArchiveImporter).Namespace, nameof(ChatArchiveImporter));

    /// <summary>
    /// Returns the entry name with forward slashes. The ZIP format prescribes them, but
    /// some tools on Windows write backslashes instead. Reading both keeps archives from
    /// such tools importable.
    /// </summary>
    private static string NormalizeEntryName(string entryName) => entryName.Replace('\\', ChatArchiveFormat.ENTRY_SEPARATOR);

    /// <summary>
    /// Reads the manifest of an archive without importing anything.
    /// </summary>
    /// <param name="archivePath">The archive to inspect.</param>
    /// <param name="token">Cancels reading the manifest.</param>
    /// <returns>The preview of the archive contents.</returns>
    public static async Task<ChatArchiveImportPreview> ReadPreviewAsync(string archivePath, CancellationToken token)
    {
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var manifestEntry = archive.GetEntry(ChatArchiveFormat.MANIFEST_FILE_NAME);
            if (manifestEntry is null)
                return new(false, new(), TB("This file is not a chat archive."));

            await using var manifestStream = manifestEntry.Open();
            var manifest = await JsonSerializer.DeserializeAsync<ChatArchiveManifest>(manifestStream, WorkspaceBehaviour.JSON_OPTIONS, token);
            if (manifest is null)
                return new(false, new(), TB("The content list of this archive could not be read."));

            if (manifest.FormatVersion > ChatArchiveFormat.FORMAT_VERSION)
                return new(false, manifest, TB("This archive was created by a newer version of AI Studio. Please update AI Studio to import it."));

            return new(true, manifest, string.Empty);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            return new(false, new(), TB("This file is not a chat archive."));
        }
        catch (Exception exception)
        {
            LOG.LogError(exception, "Failed to read the archive '{ArchivePath}'.", archivePath);
            return new(false, new(), string.Format(TB("Unexpected error: {0}"), exception.Message));
        }
    }

    /// <summary>
    /// Imports all chats of an archive into the chat storage.
    /// </summary>
    /// <param name="archivePath">The archive to import.</param>
    /// <param name="collisionBehavior">What to do with chats which already exist.</param>
    /// <param name="progress">Receives the import progress.</param>
    /// <param name="token">Cancels the import. Chats imported before the cancellation are kept.</param>
    /// <returns>The import result.</returns>
    public static async Task<ChatArchiveImportResult> ImportAsync(string archivePath, ChatArchiveCollisionBehavior collisionBehavior, IProgress<ChatArchiveProgress>? progress, CancellationToken token)
    {
        var importedWorkspaces = 0;
        var importedChats = 0;
        var skippedChats = 0;
        var failedChats = 0;

        var preview = await ReadPreviewAsync(archivePath, token);
        if (!preview.Success)
            return new(false, 0, 0, 0, 0, preview.Issue, false);

        try
        {
            var manifest = preview.Manifest;
            var totalChats = manifest.TotalChatCount;
            var processedChats = 0;

            using var archive = ZipFile.OpenRead(archivePath);
            var entriesByChat = GroupEntriesByChat(archive);

            // Which workspaces exist already is asked once per archived workspace, so read
            // the tree once instead of building a full snapshot every time:
            var tree = await WorkspaceBehaviour.GetOrLoadWorkspaceTreeShellAsync();
            var knownWorkspaceIds = tree.Workspaces.Select(workspace => workspace.WorkspaceId).ToHashSet();

            //
            // A chat counts as existing wherever it is stored, not just in the workspace the
            // archive names: it may have been moved since the export. Importing it there again
            // under the same identity would leave two chats with one ID, while the app finds a
            // chat, and the answer being written for it, by that ID alone:
            //
            var existingChatIds = tree.TemporaryChats.Select(chat => chat.ChatId).ToHashSet();
            foreach (var workspace in tree.Workspaces)
            {
                token.ThrowIfCancellationRequested();
                foreach (var chat in await WorkspaceBehaviour.GetWorkspaceChatsAsync(workspace.WorkspaceId))
                    existingChatIds.Add(chat.ChatId);
            }

            //
            // Workspace chats and temporary chats differ only in where they are stored.
            // Every counter is raised inside the loop: a cancellation leaves the chats
            // imported so far in the storage, so the numbers have to describe them even
            // when the loop never reaches its end.
            //
            async Task ImportChatsAsync(string entryPrefix, Guid targetWorkspaceId, IReadOnlyList<ChatArchiveManifestChat> chats, bool countsAsWorkspace)
            {
                var isFirstImportOfWorkspace = true;
                foreach (var chat in chats)
                {
                    token.ThrowIfCancellationRequested();
                    progress?.Report(new(processedChats, totalChats));
                    processedChats++;

                    var chatEntryPrefix = $"{entryPrefix}{chat.ChatId}{ChatArchiveFormat.ENTRY_SEPARATOR}";
                    var chatEntries = entriesByChat.GetValueOrDefault(chatEntryPrefix, []);

                    switch (await ImportChatAsync(chatEntries, chatEntryPrefix, targetWorkspaceId, chat, collisionBehavior, existingChatIds, token))
                    {
                        case ChatArchiveImportOutcome.IMPORTED:
                            importedChats++;
                            if (countsAsWorkspace && isFirstImportOfWorkspace)
                            {
                                importedWorkspaces++;
                                isFirstImportOfWorkspace = false;
                            }

                            break;

                        case ChatArchiveImportOutcome.SKIPPED:
                            skippedChats++;
                            break;

                        default:
                            failedChats++;
                            break;
                    }
                }
            }

            foreach (var workspace in manifest.Workspaces)
            {
                token.ThrowIfCancellationRequested();

                var targetWorkspaceId = await ResolveTargetWorkspaceAsync(workspace, knownWorkspaceIds);
                if (targetWorkspaceId == Guid.Empty)
                {
                    LOG.LogWarning("Skipping workspace '{WorkspaceName}' of the archive, because no target workspace could be prepared.", workspace.Name);
                    failedChats += workspace.Chats.Count;
                    processedChats += workspace.Chats.Count;
                    continue;
                }

                var workspaceEntryPrefix = $"{ChatArchiveFormat.WORKSPACES_DIRECTORY}{ChatArchiveFormat.ENTRY_SEPARATOR}{workspace.WorkspaceId}{ChatArchiveFormat.ENTRY_SEPARATOR}";
                await ImportChatsAsync(workspaceEntryPrefix, targetWorkspaceId, workspace.Chats, countsAsWorkspace: true);
            }

            var temporaryChatsEntryPrefix = $"{ChatArchiveFormat.TEMPORARY_CHATS_DIRECTORY}{ChatArchiveFormat.ENTRY_SEPARATOR}";
            await ImportChatsAsync(temporaryChatsEntryPrefix, Guid.Empty, manifest.TemporaryChats, countsAsWorkspace: false);

            progress?.Report(new(processedChats, totalChats));
            LOG.LogInformation("Imported {ImportedChats} chats from the archive '{ArchivePath}'. Skipped {SkippedChats} existing chats, {FailedChats} chats failed.", importedChats, archivePath, skippedChats, failedChats);

            return new(true, importedWorkspaces, importedChats, skippedChats, failedChats, string.Empty, false);
        }
        catch (OperationCanceledException)
        {
            //
            // Unlike a cancelled export, which leaves no archive behind, the chats imported
            // so far stay in the chat storage. So report what happened instead of throwing,
            // otherwise the user has no idea what is now part of their chats:
            //
            LOG.LogInformation("The user cancelled the import of the archive '{ArchivePath}' after {ImportedChats} chats.", archivePath, importedChats);
            return new(true, importedWorkspaces, importedChats, skippedChats, failedChats, string.Empty, true);
        }
        catch (Exception exception)
        {
            LOG.LogError(exception, "Failed to import the archive '{ArchivePath}'.", archivePath);
            return new(false, importedWorkspaces, importedChats, skippedChats, failedChats, string.Format(TB("Unexpected error: {0}"), exception.Message), false);
        }
    }

    /// <summary>
    /// Sorts all archive entries by the chat they belong to. Without this, every chat would
    /// have to scan all entries of the archive.
    /// </summary>
    private static Dictionary<string, List<ZipArchiveEntry>> GroupEntriesByChat(ZipArchive archive)
    {
        var entriesByChat = new Dictionary<string, List<ZipArchiveEntry>>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            var entryName = NormalizeEntryName(entry.FullName);

            // Skip directory entries:
            if (entryName.EndsWith(ChatArchiveFormat.ENTRY_SEPARATOR))
                continue;

            var chatEntryPrefix = GetChatEntryPrefix(entryName);
            if (chatEntryPrefix is null)
                continue;

            if (!entriesByChat.TryGetValue(chatEntryPrefix, out var chatEntries))
            {
                chatEntries = [];
                entriesByChat[chatEntryPrefix] = chatEntries;
            }

            chatEntries.Add(entry);
        }

        return entriesByChat;
    }

    /// <summary>
    /// Determines the chat an archive entry belongs to: everything up to and including the
    /// chat identifier, so <c>workspaces/W/C/thread.json</c> becomes <c>workspaces/W/C/</c>.
    /// </summary>
    /// <returns>The chat prefix, or null when the entry belongs to no chat.</returns>
    private static string? GetChatEntryPrefix(string entryName)
    {
        var expectedSeparators = 0;
        if (entryName.StartsWith($"{ChatArchiveFormat.WORKSPACES_DIRECTORY}{ChatArchiveFormat.ENTRY_SEPARATOR}", StringComparison.Ordinal))
            expectedSeparators = 3;
        else if (entryName.StartsWith($"{ChatArchiveFormat.TEMPORARY_CHATS_DIRECTORY}{ChatArchiveFormat.ENTRY_SEPARATOR}", StringComparison.Ordinal))
            expectedSeparators = 2;

        if (expectedSeparators is 0)
            return null;

        var index = 0;
        for (var separator = 0; separator < expectedSeparators; separator++)
        {
            var next = entryName.IndexOf(ChatArchiveFormat.ENTRY_SEPARATOR, index);
            if (next < 0)
                return null;

            index = next + 1;
        }

        return entryName[..index];
    }

    /// <summary>
    /// Determines the workspace which receives the chats of an archived workspace. Known
    /// workspaces are reused, unknown ones are created with their original identity.
    /// </summary>
    /// <param name="workspace">The archived workspace.</param>
    /// <param name="knownWorkspaceIds">The workspaces which exist already. Newly created ones are added.</param>
    private static async Task<Guid> ResolveTargetWorkspaceAsync(ChatArchiveManifestWorkspace workspace, HashSet<Guid> knownWorkspaceIds)
    {
        if (workspace.WorkspaceId == Guid.Empty)
            return Guid.Empty;

        if (knownWorkspaceIds.Contains(workspace.WorkspaceId))
            return workspace.WorkspaceId;

        var workspaceName = string.IsNullOrWhiteSpace(workspace.Name) ? TB("Imported workspace") : workspace.Name;
        await WorkspaceBehaviour.EnsureWorkspace(workspace.WorkspaceId, await CreateUniqueWorkspaceNameAsync(workspaceName));
        knownWorkspaceIds.Add(workspace.WorkspaceId);

        return workspace.WorkspaceId;
    }

    /// <summary>
    /// Extends the workspace name by a counter until it is no longer taken. Workspace names
    /// must be unique, otherwise the workspace cannot be renamed later on.
    /// </summary>
    private static async Task<string> CreateUniqueWorkspaceNameAsync(string workspaceName)
    {
        var candidate = workspaceName;
        var counter = 1;

        while (await WorkspaceBehaviour.IsWorkspaceNameExistingAsync(candidate))
        {
            counter++;
            candidate = $"{workspaceName} ({counter})";
        }

        return candidate;
    }

    /// <summary>
    /// Imports one chat: extracts its files into a fresh chat directory, turns the
    /// archive-relative attachment paths back into absolute ones, and stores the chat.
    /// </summary>
    /// <param name="existingChatIds">The IDs of all chats in the storage. An imported chat is added.</param>
    private static async Task<ChatArchiveImportOutcome> ImportChatAsync(IReadOnlyList<ZipArchiveEntry> chatEntries, string chatEntryPrefix, Guid targetWorkspaceId, ChatArchiveManifestChat chat, ChatArchiveCollisionBehavior collisionBehavior, HashSet<Guid> existingChatIds, CancellationToken token)
    {
        var threadEntry = chatEntries.FirstOrDefault(entry => NormalizeEntryName(entry.FullName).Equals($"{chatEntryPrefix}{ChatArchiveFormat.THREAD_FILE_NAME}", StringComparison.OrdinalIgnoreCase));
        if (threadEntry is null)
        {
            LOG.LogWarning("The archive contains no chat thread for the chat '{ChatId}'.", chat.ChatId);
            return ChatArchiveImportOutcome.FAILED;
        }

        //
        // The directory is checked as well: a chat the storage could not list, for example
        // because its thread is unreadable, still occupies its place on disk.
        //
        var targetChatId = chat.ChatId;
        if (existingChatIds.Contains(targetChatId) || WorkspaceBehaviour.IsChatExisting(new(targetWorkspaceId, targetChatId)))
        {
            if (collisionBehavior is ChatArchiveCollisionBehavior.SKIP)
                return ChatArchiveImportOutcome.SKIPPED;

            targetChatId = Guid.NewGuid();
        }

        var chatDirectory = WorkspaceBehaviour.GetChatDirectory(targetWorkspaceId, targetChatId);
        try
        {
            Directory.CreateDirectory(chatDirectory);

            //
            // Extract everything except the chat thread and the name file. Both are written
            // by the chat storage itself once the paths inside the thread are corrected:
            //
            var extractedPaths = new HashSet<string>(PathTools.COMPARER);
            foreach (var entry in chatEntries)
            {
                token.ThrowIfCancellationRequested();

                var relativePath = NormalizeEntryName(entry.FullName)[chatEntryPrefix.Length..];
                if (relativePath.Equals(ChatArchiveFormat.THREAD_FILE_NAME, StringComparison.OrdinalIgnoreCase) ||
                    relativePath.Equals(ChatArchiveFormat.NAME_FILE_NAME, StringComparison.OrdinalIgnoreCase))
                    continue;

                var destinationPath = ResolveSafeDestination(chatDirectory, relativePath);
                if (destinationPath is null)
                {
                    LOG.LogWarning("Ignoring the archive entry '{EntryName}', because it points outside of the chat directory.", entry.FullName);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

                await using var entryStream = entry.Open();
                await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, BUFFER_SIZE, FileOptions.Asynchronous | FileOptions.SequentialScan);
                await entryStream.CopyToAsync(fileStream, token);

                extractedPaths.Add(relativePath);
            }

            ChatThread? thread;
            await using (var threadStream = threadEntry.Open())
                thread = await JsonSerializer.DeserializeAsync<ChatThread>(threadStream, WorkspaceBehaviour.JSON_OPTIONS, token);

            if (thread is null)
            {
                LOG.LogWarning("The chat thread of the chat '{ChatId}' could not be read.", chat.ChatId);
                TryDeleteDirectory(chatDirectory);
                return ChatArchiveImportOutcome.FAILED;
            }

            thread = thread with { ChatId = targetChatId };
            thread.WorkspaceId = targetWorkspaceId;

            //
            // Turn the archive-relative paths back into absolute ones. Only paths whose files
            // were actually extracted are rewritten. Everything else was already absolute when
            // the archive was created and stays as it is:
            //
            ChatArchiveAttachmentPaths.Rewrite(thread, path =>
            {
                var normalizedPath = path.Replace(Path.DirectorySeparatorChar, ChatArchiveFormat.ENTRY_SEPARATOR);
                if (!extractedPaths.Contains(normalizedPath))
                    return path;

                return Path.Combine(chatDirectory, normalizedPath.Replace(ChatArchiveFormat.ENTRY_SEPARATOR, Path.DirectorySeparatorChar));
            });

            CopyTranscriptsStoredElsewhere(thread, chatDirectory);

            //
            // The storage skips a chat whose lock another operation holds for too long. The
            // directory prepared above would then hold the extracted files but no chat:
            //
            if (!await WorkspaceBehaviour.StoreChatAsync(thread))
            {
                LOG.LogWarning("The chat '{ChatId}' could not be stored, because another operation held it for too long.", chat.ChatId);
                TryDeleteDirectory(chatDirectory);
                return ChatArchiveImportOutcome.FAILED;
            }

            existingChatIds.Add(targetChatId);
            return ChatArchiveImportOutcome.IMPORTED;
        }
        catch (OperationCanceledException)
        {
            TryDeleteDirectory(chatDirectory);
            throw;
        }
        catch (Exception exception)
        {
            LOG.LogError(exception, "Failed to import the chat '{ChatId}' into the workspace '{WorkspaceId}'.", chat.ChatId, targetWorkspaceId);
            TryDeleteDirectory(chatDirectory);
            return ChatArchiveImportOutcome.FAILED;
        }
    }

    /// <summary>
    /// Gives the imported chat its own copy of every transcript the archive did not carry.
    /// </summary>
    /// <remarks>
    /// Such a transcript still names the place where it lay when the archive was made. On the
    /// same computer that is the chat the archive came from, and since a chat deletes the
    /// transcripts it names, deleting the imported chat would take the transcripts of the
    /// original along. A copied chat gets its own copies for the same reason, so this uses the
    /// same code. A transcript which no longer exists keeps its place in the chat and shows up
    /// as missing, again as it does in a copy.
    /// </remarks>
    /// <param name="thread">The imported chat thread, after its archive-relative paths were resolved.</param>
    /// <param name="chatDirectory">The directory of the imported chat.</param>
    private static void CopyTranscriptsStoredElsewhere(ChatThread thread, string chatDirectory)
    {
        var transcriptDirectory = Path.Combine(chatDirectory, "attachments", "transcripts");
        var copiedPaths = new Dictionary<string, ManagedTranscriptAttachment>(PathTools.COMPARER);

        //
        // Only a transcript in the data directory can belong to another chat, and only there
        // does deleting a chat delete its transcripts. Any other path is left alone, so that an
        // archive cannot get files from elsewhere on this computer copied into a chat:
        //
        ManagedTranscriptAttachment CopyWhenElsewhere(ManagedTranscriptAttachment transcript) =>
            IsInsideDirectory(chatDirectory, transcript.FilePath) || !IsInsideDirectory(SettingsManager.DataDirectory!, transcript.FilePath)
                ? transcript
                : WorkspaceBehaviour.CopyManagedTranscriptAttachment(thread, transcript, transcriptDirectory, copiedPaths);

        foreach (var content in thread.Blocks.Select(block => block.Content).OfType<IContent>())
        {
            for (var index = 0; index < content.FileAttachments.Count; index++)
            {
                if (content.FileAttachments[index] is ManagedTranscriptAttachment transcript)
                    content.FileAttachments[index] = CopyWhenElsewhere(transcript);
            }
        }

        for (var index = 0; index < thread.PendingMediaTranscripts.Count; index++)
            thread.PendingMediaTranscripts[index] = CopyWhenElsewhere(thread.PendingMediaTranscripts[index]);
    }

    /// <summary>
    /// Determines whether a path lies below the given directory.
    /// </summary>
    /// <remarks>
    /// A path which cannot be resolved counts as lying elsewhere. The copy which follows then
    /// decides what becomes of it, and it keeps such an attachment as it is.
    /// </remarks>
    private static bool IsInsideDirectory(string directory, string path)
    {
        try
        {
            var fullDirectory = Path.GetFullPath(directory);
            var directoryPrefix = fullDirectory.EndsWith(Path.DirectorySeparatorChar)
                ? fullDirectory
                : fullDirectory + Path.DirectorySeparatorChar;

            return Path.GetFullPath(path).StartsWith(directoryPrefix, PathTools.COMPARISON);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    /// Combines the chat directory with an archive-relative path and rejects paths which
    /// would escape the chat directory.
    /// </summary>
    private static string? ResolveSafeDestination(string chatDirectory, string relativePath)
    {
        try
        {
            var fullChatDirectory = Path.GetFullPath(chatDirectory);
            var destinationPath = Path.GetFullPath(Path.Combine(fullChatDirectory, relativePath.Replace(ChatArchiveFormat.ENTRY_SEPARATOR, Path.DirectorySeparatorChar)));

            var directoryPrefix = fullChatDirectory.EndsWith(Path.DirectorySeparatorChar)
                ? fullChatDirectory
                : fullChatDirectory + Path.DirectorySeparatorChar;

            return destinationPath.StartsWith(directoryPrefix, PathTools.COMPARISON) ? destinationPath : null;
        }
        catch (Exception exception)
        {
            LOG.LogWarning(exception, "Could not resolve the archive path '{RelativePath}' inside the chat directory '{ChatDirectory}'.", relativePath, chatDirectory);
            return null;
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            return;

        try
        {
            Directory.Delete(directory, true);
        }
        catch (Exception exception)
        {
            LOG.LogWarning(exception, "Failed to clean up the incomplete chat directory '{ChatDirectory}'.", directory);
        }
    }
}