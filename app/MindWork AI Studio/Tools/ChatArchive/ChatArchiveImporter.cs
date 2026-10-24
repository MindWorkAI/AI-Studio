using System.IO.Compression;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tools.Databases.ProvenanceStore;
using AIStudio.Tools.DataBackup;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.ChatArchive;

/// <summary>
/// Adds the chats of a backup to the chats there are, and leaves everything else alone.
/// </summary>
/// <remarks>
/// The other way to use a backup, next to restoring it: a restore replaces all data, while this keeps
/// every chat and adds those of the backup. A chat which exists already is kept, or added once more,
/// as the user decides.<br/><br/>
///
/// The backup is opened for reading only, and every entry is read as a stream, cf. DataBackupReader.
/// </remarks>
public static class ChatArchiveImporter
{
    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger(nameof(ChatArchiveImporter));

    private const int BUFFER_SIZE = 64 * 1024;

    /// <summary>
    /// What SQLite appends to the name of a database for its write-ahead log.
    /// </summary>
    private const string WRITE_AHEAD_LOG_SUFFIX = "-wal";

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ChatArchiveImporter).Namespace, nameof(ChatArchiveImporter));

    /// <summary>
    /// One entry of a backup, with its name checked and normalized.
    /// </summary>
    private sealed record BackupEntry(string Name, ZipArchiveEntry Entry);

    /// <summary>
    /// Where the imported chats get what they received besides their messages from.
    /// </summary>
    /// <param name="Store">The provenance store of this computer, which receives the entries.</param>
    /// <param name="BackupStorePath">The provenance store of the backup, unpacked into a file of its own.</param>
    private sealed record ProvenanceImport(ProvenanceStoreClient Store, string BackupStorePath);

    /// <summary>
    /// What one import did so far.
    /// </summary>
    /// <remarks>
    /// Every counter is raised as the import goes: a cancellation leaves the chats imported so far in
    /// the storage, so the numbers have to describe them even when the import never reaches its end.
    /// </remarks>
    private sealed class ImportTally
    {
        public int ProcessedChats;
        public int ImportedWorkspaces;
        public int ImportedChats;
        public int SkippedChats;
        public int FailedChats;
    }

    /// <summary>
    /// Reads which chats a backup holds, without adding any.
    /// </summary>
    /// <param name="archivePath">The backup to look into.</param>
    /// <param name="token">Cancels the reading.</param>
    /// <returns>The chats of the backup, or why the backup cannot be used.</returns>
    public static async Task<ChatArchiveImportPreview> ReadPreviewAsync(string archivePath, CancellationToken token)
    {
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var manifestResult = await DataBackupReader.ReadManifestAsync(archive, token);
            if (manifestResult.Manifest is not { } manifest)
                return new(false, new(), manifestResult.Issue);

            var chats = await ListChatsAsync(archive, GroupEntriesByChat(archive), token);
            return new(true, chats with { CreatedAt = manifest.CreatedAtUtc, AppVersion = manifest.AppVersion }, string.Empty);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            return new(false, new(), TB("This file is no backup of AI Studio, or it was not written completely."));
        }
        catch (Exception exception)
        {
            LOG.LogError(exception, "Failed to read the backup '{ArchivePath}'.", archivePath);
            return new(false, new(), string.Format(TB("Unexpected error: {0}"), exception.Message));
        }
    }

    /// <summary>
    /// Adds all chats of a backup to the chat storage.
    /// </summary>
    /// <param name="archivePath">The backup.</param>
    /// <param name="provenanceStore">The provenance store, which receives what the imported chats received besides their messages.</param>
    /// <param name="collisionBehavior">What to do with chats which exist already.</param>
    /// <param name="progress">Receives the progress.</param>
    /// <param name="token">Cancels the import. Chats added before the cancellation are kept.</param>
    /// <returns>What the import did.</returns>
    public static async Task<ChatArchiveImportResult> ImportAsync(string archivePath, ProvenanceStoreClient provenanceStore, ChatArchiveCollisionBehavior collisionBehavior, IProgress<ChatArchiveProgress>? progress, CancellationToken token)
    {
        var tally = new ImportTally();
        var preview = await ReadPreviewAsync(archivePath, token);
        if (!preview.Success)
            return new(false, 0, 0, 0, 0, preview.Issue, false);

        // Next to the copies a backup makes, which the next start clears in case AI Studio stops halfway:
        var provenanceDirectory = Path.Join(DataRestoreFormat.DirectoryIn(SettingsManager.DataDirectory!, DataBackupFormat.STAGING_DIRECTORY), $"provenance-import-{Guid.NewGuid():N}");
        try
        {
            var manifest = preview.Manifest;
            using var archive = ZipFile.OpenRead(archivePath);
            var entriesByChat = GroupEntriesByChat(archive);
            var provenance = await ExtractProvenanceStoreAsync(archive, provenanceStore, provenanceDirectory, token);

            // Which workspaces exist already is asked once per workspace of the backup, so read
            // the tree once instead of building a full snapshot every time:
            var tree = await WorkspaceBehaviour.GetOrLoadWorkspaceTreeShellAsync();
            var knownWorkspaceIds = tree.Workspaces.Select(workspace => workspace.WorkspaceId).ToHashSet();

            //
            // A chat counts as existing wherever it is stored, not just in the workspace the backup
            // names: it may have been moved since the backup was made. Adding it there again under
            // the same identity would leave two chats with one ID, while the app finds a chat, and
            // the answer being written for it, by that ID alone:
            //
            var existingChatIds = tree.TemporaryChats.Select(chat => chat.ChatId).ToHashSet();
            foreach (var workspace in tree.Workspaces)
            {
                token.ThrowIfCancellationRequested();
                foreach (var chat in await WorkspaceBehaviour.GetWorkspaceChatsAsync(workspace.WorkspaceId))
                    existingChatIds.Add(chat.ChatId);
            }

            foreach (var workspace in manifest.Workspaces)
            {
                token.ThrowIfCancellationRequested();

                var targetWorkspaceId = await ResolveTargetWorkspaceAsync(workspace, knownWorkspaceIds);
                if (targetWorkspaceId == Guid.Empty)
                {
                    LOG.LogWarning("Skipping workspace '{WorkspaceName}' of the backup, because no target workspace could be prepared.", workspace.Name);
                    tally.FailedChats += workspace.Chats.Count;
                    tally.ProcessedChats += workspace.Chats.Count;
                    continue;
                }

                var workspaceEntryPrefix = $"{ChatArchiveFormat.WORKSPACES_PREFIX}{workspace.WorkspaceId}{ZipEntryNames.SEPARATOR}";
                await ImportChatsAsync(tally, workspaceEntryPrefix, targetWorkspaceId, workspace.Chats, true, manifest.TotalChatCount, entriesByChat, collisionBehavior, existingChatIds, provenance, progress, token);
            }

            await ImportChatsAsync(tally, ChatArchiveFormat.TEMPORARY_CHATS_PREFIX, Guid.Empty, manifest.TemporaryChats, false, manifest.TotalChatCount, entriesByChat, collisionBehavior, existingChatIds, provenance, progress, token);

            progress?.Report(new(tally.ProcessedChats, manifest.TotalChatCount));
            LOG.LogInformation("Imported {ImportedChats} chats from the backup '{ArchivePath}'. Skipped {SkippedChats} existing chats, {FailedChats} chats failed.", tally.ImportedChats, archivePath, tally.SkippedChats, tally.FailedChats);

            return new(true, tally.ImportedWorkspaces, tally.ImportedChats, tally.SkippedChats, tally.FailedChats, string.Empty, false);
        }
        catch (OperationCanceledException)
        {
            //
            // The chats imported so far stay in the chat storage. So report what happened instead
            // of throwing, otherwise the user has no idea what is now part of their chats:
            //
            LOG.LogInformation("The user cancelled the import of the backup '{ArchivePath}' after {ImportedChats} chats.", archivePath, tally.ImportedChats);
            return new(true, tally.ImportedWorkspaces, tally.ImportedChats, tally.SkippedChats, tally.FailedChats, string.Empty, true);
        }
        catch (Exception exception)
        {
            LOG.LogError(exception, "Failed to import the backup '{ArchivePath}'.", archivePath);
            return new(false, tally.ImportedWorkspaces, tally.ImportedChats, tally.SkippedChats, tally.FailedChats, string.Format(TB("Unexpected error: {0}"), exception.Message), false);
        }
        finally
        {
            TryDeleteDirectory(provenanceDirectory);
        }
    }

    /// <summary>
    /// Unpacks the provenance store of a backup, so that every imported chat can take its entries from it.
    /// </summary>
    /// <remarks>
    /// A backup of a version before the store holds none, and its chats arrive as they were: their
    /// sources without the contents behind them.
    /// </remarks>
    /// <param name="archive">The backup.</param>
    /// <param name="provenanceStore">The provenance store of this computer.</param>
    /// <param name="directory">The directory to unpack the store into. The caller deletes it afterward.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>Where the entries come from, or null when the backup holds no store, or this computer cannot keep one.</returns>
    private static async Task<ProvenanceImport?> ExtractProvenanceStoreAsync(ZipArchive archive, ProvenanceStoreClient provenanceStore, string directory, CancellationToken token)
    {
        if (provenanceStore is NoProvenanceStoreClient)
        {
            LOG.LogWarning("The provenance store is not available, so the chats of the backup arrive without what they received besides their messages.");
            return null;
        }

        var entry = archive.GetEntry(ChatArchiveFormat.PROVENANCE_STORE_ENTRY);
        if (entry is null)
        {
            LOG.LogInformation("The backup holds no provenance store, so its chats arrive without what they received besides their messages.");
            return null;
        }

        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Join(directory, SqliteProvenanceStoreClientImplementation.DATABASE_FILENAME);
            await ExtractEntryAsync(entry, path, token);

            //
            // A backup which took the store as it lay in the data folder may hold its newest entries
            // in the write-ahead log next to it, which SQLite reads along:
            //
            if (archive.GetEntry(ChatArchiveFormat.PROVENANCE_STORE_ENTRY + WRITE_AHEAD_LOG_SUFFIX) is { } logEntry)
                await ExtractEntryAsync(logEntry, path + WRITE_AHEAD_LOG_SUFFIX, token);

            return new(provenanceStore, path);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // As in ImportProvenanceAsync: without the store, the chats lose the contents behind their sources, not themselves.
            LOG.LogWarning(exception, "The provenance store of the backup could not be unpacked, so its chats arrive without what they received besides their messages.");
            return null;
        }
    }

    /// <summary>
    /// Writes one entry of the backup into a file, as a stream.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="destinationPath">The file, which is replaced when it exists.</param>
    /// <param name="token">The cancellation token.</param>
    private static async Task ExtractEntryAsync(ZipArchiveEntry entry, string destinationPath, CancellationToken token)
    {
        await using var entryStream = entry.Open();
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, BUFFER_SIZE, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await entryStream.CopyToAsync(fileStream, token);
    }

    /// <summary>
    /// Adds what an imported chat received besides its messages, from the provenance store of the backup.
    /// </summary>
    /// <remarks>
    /// The chat is stored already, so this ends neither by a cancellation nor by a failure: the next
    /// chat checks for the cancellation. A chat without its entries still has its messages and
    /// sources; it only lacks the contents behind them, and the store of the backup may be damaged
    /// or of a newer form.
    /// </remarks>
    /// <param name="provenance">Where the entries come from, or null when they come from nowhere.</param>
    /// <param name="sourceChatId">The chat in the backup.</param>
    /// <param name="targetChatId">The chat as it was imported, possibly under a new id.</param>
    private static async Task ImportProvenanceAsync(ProvenanceImport? provenance, Guid sourceChatId, Guid targetChatId)
    {
        if (provenance is null)
            return;

        try
        {
            var count = await provenance.Store.ImportChatAsync(provenance.BackupStorePath, sourceChatId, targetChatId, CancellationToken.None);
            LOG.LogDebug("Imported {Count} provenance entries of the chat '{ChatId}'.", count, targetChatId);
        }
        catch (Exception exception)
        {
            LOG.LogWarning(exception, "The chat '{ChatId}' was imported without what it received besides its messages, because the provenance store of the backup could not be read.", targetChatId);
        }
    }

    /// <summary>
    /// Adds the chats of one workspace of the backup, or its temporary chats.
    /// </summary>
    private static async Task ImportChatsAsync(ImportTally tally, string entryPrefix, Guid targetWorkspaceId, IReadOnlyList<ChatArchiveManifestChat> chats, bool countsAsWorkspace, int totalChats, Dictionary<string, List<BackupEntry>> entriesByChat, ChatArchiveCollisionBehavior collisionBehavior, HashSet<Guid> existingChatIds, ProvenanceImport? provenance, IProgress<ChatArchiveProgress>? progress, CancellationToken token)
    {
        var isFirstImportOfWorkspace = true;
        foreach (var chat in chats)
        {
            token.ThrowIfCancellationRequested();
            progress?.Report(new(tally.ProcessedChats, totalChats));
            tally.ProcessedChats++;

            var chatEntryPrefix = $"{entryPrefix}{chat.ChatId}{ZipEntryNames.SEPARATOR}";
            var chatEntries = entriesByChat.GetValueOrDefault(chatEntryPrefix, []);

            switch (await ImportChatAsync(chatEntries, chatEntryPrefix, targetWorkspaceId, chat, collisionBehavior, existingChatIds, provenance, token))
            {
                case ChatArchiveImportOutcome.IMPORTED:
                    tally.ImportedChats++;
                    if (countsAsWorkspace && isFirstImportOfWorkspace)
                    {
                        tally.ImportedWorkspaces++;
                        isFirstImportOfWorkspace = false;
                    }

                    break;

                case ChatArchiveImportOutcome.SKIPPED:
                    tally.SkippedChats++;
                    break;

                case ChatArchiveImportOutcome.FAILED:
                    tally.FailedChats++;
                    break;
            }
        }
    }

    /// <summary>
    /// Sorts the entries of a backup by the chat they belong to. Without this, every chat would have
    /// to scan all entries of the backup.
    /// </summary>
    /// <remarks>
    /// An entry whose name could lead outside of the chat directory is left out, cf. ZipEntryNames.
    /// </remarks>
    private static Dictionary<string, List<BackupEntry>> GroupEntriesByChat(ZipArchive archive)
    {
        var entriesByChat = new Dictionary<string, List<BackupEntry>>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            if (!ZipEntryNames.TryNormalize(entry.FullName, out var entryName))
            {
                LOG.LogWarning("Ignoring the backup entry '{EntryName}', because its name could lead outside of the chat directory.", entry.FullName);
                continue;
            }

            // Skip directory entries:
            if (entryName.EndsWith(ZipEntryNames.SEPARATOR))
                continue;

            var chatEntryPrefix = GetChatEntryPrefix(entryName);
            if (chatEntryPrefix is null)
                continue;

            if (!entriesByChat.TryGetValue(chatEntryPrefix, out var chatEntries))
            {
                chatEntries = [];
                entriesByChat[chatEntryPrefix] = chatEntries;
            }

            chatEntries.Add(new(entryName, entry));
        }

        return entriesByChat;
    }

    /// <summary>
    /// Determines the chat an entry belongs to: everything up to and including the ID of the chat, so
    /// "data/workspaces/W/C/thread.json" becomes "data/workspaces/W/C/".
    /// </summary>
    /// <returns>The chat prefix, or null when the entry belongs to no chat.</returns>
    private static string? GetChatEntryPrefix(string entryName)
    {
        string root;
        int levels;
        if (entryName.StartsWith(ChatArchiveFormat.WORKSPACES_PREFIX, StringComparison.Ordinal))
        {
            root = ChatArchiveFormat.WORKSPACES_PREFIX;
            levels = 2;
        }
        else if (entryName.StartsWith(ChatArchiveFormat.TEMPORARY_CHATS_PREFIX, StringComparison.Ordinal))
        {
            root = ChatArchiveFormat.TEMPORARY_CHATS_PREFIX;
            levels = 1;
        }
        else
            return null;

        var index = root.Length;
        for (var level = 0; level < levels; level++)
        {
            var next = entryName.IndexOf(ZipEntryNames.SEPARATOR, index);
            if (next < 0)
                return null;

            index = next + 1;
        }

        return entryName[..index];
    }

    /// <summary>
    /// Lists the chats of a backup, with the names of their workspaces.
    /// </summary>
    private static async Task<ChatArchiveManifest> ListChatsAsync(ZipArchive archive, Dictionary<string, List<BackupEntry>> entriesByChat, CancellationToken token)
    {
        var workspaces = new Dictionary<Guid, ChatArchiveManifestWorkspace>();
        var temporaryChats = new List<ChatArchiveManifestChat>();
        foreach (var (chatEntryPrefix, chatEntries) in entriesByChat)
        {
            token.ThrowIfCancellationRequested();

            // A chat without its thread cannot be added, so it is not listed either:
            if (!chatEntries.Any(entry => entry.Name.Equals($"{chatEntryPrefix}{ChatArchiveFormat.THREAD_FILE_NAME}", StringComparison.OrdinalIgnoreCase)))
                continue;

            var segments = chatEntryPrefix.TrimEnd(ZipEntryNames.SEPARATOR).Split(ZipEntryNames.SEPARATOR);
            if (!Guid.TryParse(segments[^1], out var chatId))
                continue;

            var nameEntry = chatEntries.FirstOrDefault(entry => entry.Name.Equals($"{chatEntryPrefix}{ChatArchiveFormat.NAME_FILE_NAME}", StringComparison.OrdinalIgnoreCase));
            var chat = new ChatArchiveManifestChat
            {
                ChatId = chatId,
                Name = nameEntry is null ? string.Empty : await ReadTextAsync(nameEntry.Entry, token),
            };

            if (chatEntryPrefix.StartsWith(ChatArchiveFormat.TEMPORARY_CHATS_PREFIX, StringComparison.Ordinal))
            {
                temporaryChats.Add(chat);
                continue;
            }

            if (!Guid.TryParse(segments[^2], out var workspaceId))
                continue;

            if (!workspaces.TryGetValue(workspaceId, out var workspace))
            {
                var workspaceNameEntry = archive.GetEntry($"{ChatArchiveFormat.WORKSPACES_PREFIX}{workspaceId}{ZipEntryNames.SEPARATOR}{ChatArchiveFormat.NAME_FILE_NAME}");
                workspace = new ChatArchiveManifestWorkspace
                {
                    WorkspaceId = workspaceId,
                    Name = workspaceNameEntry is null ? string.Empty : await ReadTextAsync(workspaceNameEntry, token),
                };

                workspaces[workspaceId] = workspace;
            }

            workspace.Chats.Add(chat);
        }

        return new()
        {
            Workspaces = workspaces.Values.OrderBy(workspace => workspace.Name, StringComparer.CurrentCultureIgnoreCase).ToList(),
            TemporaryChats = temporaryChats,
        };
    }

    private static async Task<string> ReadTextAsync(ZipArchiveEntry entry, CancellationToken token)
    {
        using var reader = new StreamReader(entry.Open());
        return (await reader.ReadToEndAsync(token)).Trim();
    }

    /// <summary>
    /// Determines the workspace which receives the chats of a workspace of the backup. Known
    /// workspaces are reused, unknown ones are created with their original identity.
    /// </summary>
    /// <param name="workspace">The workspace of the backup.</param>
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
    /// Imports one chat: extracts its files into a fresh chat directory, points the paths of
    /// those files to it, and stores the chat.
    /// </summary>
    /// <param name="chatEntries">The backup entries of the chat.</param>
    /// <param name="chatEntryPrefix">The path of the chat within the backup, which every entry of the chat starts with.</param>
    /// <param name="targetWorkspaceId">The workspace to import the chat into, or Guid.Empty for a temporary chat.</param>
    /// <param name="chat">The chat as the preview lists it.</param>
    /// <param name="collisionBehavior">What to do when a chat with the same ID exists already.</param>
    /// <param name="existingChatIds">The IDs of all chats in the storage. An imported chat is added.</param>
    /// <param name="provenance">Where the chat gets what it received besides its messages from, or null when from nowhere.</param>
    /// <param name="token">Cancels the import.</param>
    private static async Task<ChatArchiveImportOutcome> ImportChatAsync(IReadOnlyList<BackupEntry> chatEntries, string chatEntryPrefix, Guid targetWorkspaceId, ChatArchiveManifestChat chat, ChatArchiveCollisionBehavior collisionBehavior, HashSet<Guid> existingChatIds, ProvenanceImport? provenance, CancellationToken token)
    {
        var threadEntry = chatEntries.FirstOrDefault(entry => entry.Name.Equals($"{chatEntryPrefix}{ChatArchiveFormat.THREAD_FILE_NAME}", StringComparison.OrdinalIgnoreCase));
        if (threadEntry is null)
        {
            LOG.LogWarning("The backup contains no chat thread for the chat '{ChatId}'.", chat.ChatId);
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

                var relativePath = entry.Name[chatEntryPrefix.Length..];
                if (relativePath.Equals(ChatArchiveFormat.THREAD_FILE_NAME, StringComparison.OrdinalIgnoreCase) ||
                    relativePath.Equals(ChatArchiveFormat.NAME_FILE_NAME, StringComparison.OrdinalIgnoreCase))
                    continue;

                var destinationPath = ZipEntryNames.GetDestinationPath(chatDirectory, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                await ExtractEntryAsync(entry.Entry, destinationPath, token);

                extractedPaths.Add(relativePath);
            }

            ChatThread? thread;
            await using (var threadStream = threadEntry.Entry.Open())
                thread = await JsonSerializer.DeserializeAsync<ChatThread>(threadStream, WorkspaceBehaviour.JSON_OPTIONS, token);

            if (thread is null)
            {
                LOG.LogWarning("The chat thread of the chat '{ChatId}' could not be read.", chat.ChatId);
                TryDeleteDirectory(chatDirectory);
                return ChatArchiveImportOutcome.FAILED;
            }

            //
            // A file below the data directory is stored with a path relative to it, which reading
            // the thread resolved against the data directory of this computer: where the chat lay
            // when the backup was made. Only the paths of files which were actually extracted move
            // to the imported chat. Everything else keeps its path:
            //
            var originalChatDirectory = WorkspaceBehaviour.GetChatDirectory(thread.WorkspaceId, thread.ChatId);
            thread = thread with { ChatId = targetChatId };
            thread.WorkspaceId = targetWorkspaceId;
            thread.RewriteFilePaths(path =>
            {
                var chatRelativePath = Path.GetRelativePath(originalChatDirectory, path).Replace(Path.DirectorySeparatorChar, ZipEntryNames.SEPARATOR);
                return extractedPaths.Contains(chatRelativePath)
                    ? Path.Combine(chatDirectory, chatRelativePath.Replace(ZipEntryNames.SEPARATOR, Path.DirectorySeparatorChar))
                    : path;
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

            // Under the id of the chat in the backup, which is what the store of the backup knows it by:
            await ImportProvenanceAsync(provenance, chat.ChatId, targetChatId);
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
    /// Gives the imported chat its own copy of every transcript the backup did not carry.
    /// </summary>
    /// <remarks>
    /// Such a transcript still names the place where it lay when the backup was made. On the
    /// same computer that is the chat the backup came from, and since a chat deletes the
    /// transcripts it names, deleting the imported chat would take the transcripts of the
    /// original along. A copied chat gets its own copies for the same reason, so this uses the
    /// same code. A transcript which no longer exists keeps its place in the chat and shows up
    /// as missing, again as it does in a copy.
    /// </remarks>
    /// <param name="thread">The imported chat thread, after the paths of its extracted files were pointed to it.</param>
    /// <param name="chatDirectory">The directory of the imported chat.</param>
    private static void CopyTranscriptsStoredElsewhere(ChatThread thread, string chatDirectory)
    {
        var transcriptDirectory = ManagedTranscriptAttachment.GetTranscriptDirectory(chatDirectory);
        var copiedPaths = new Dictionary<string, ManagedTranscriptAttachment>(PathTools.COMPARER);

        //
        // Only a transcript in the data directory can belong to another chat, and only there
        // does deleting a chat delete its transcripts. Any other path is left alone, so that a
        // backup cannot get files from elsewhere on this computer copied into a chat:
        //
        thread.ReplaceAttachments(attachment => attachment is ManagedTranscriptAttachment transcript
                                                && !PathTools.IsInsideDirectory(chatDirectory, transcript.FilePath)
                                                && PathTools.IsInsideDirectory(SettingsManager.DataDirectory!, transcript.FilePath)
            ? WorkspaceBehaviour.CopyManagedTranscriptAttachment(thread, transcript, transcriptDirectory, copiedPaths)
            : attachment);
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
            LOG.LogWarning(exception, "Failed to clean up the directory '{Directory}'.", directory);
        }
    }
}